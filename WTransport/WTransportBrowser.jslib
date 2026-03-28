const WTransport = {
    // -------------------------------------------------------------------------
    // INTERNAL STATE & LOGIC ($WT)
    // -------------------------------------------------------------------------
    $WT: {
        activeTransport: null,
        writer: null,
        onMessageCallback: null,
        onStreamMessageCallback: null, // [NEW] Callback for reliable streams
        onDisconnectedCallback: null,
        onOpenCallback: null,

        // --- Helpers ---

        CleanupConnection: function(reason) {
            if (!WT.activeTransport) return;

            console.log(`[JS] WebTransport Closed: ${reason}`);
            WT.activeTransport = null;
            WT.writer = null;

            if (WT.onDisconnectedCallback) {
                try { dynCall('v', WT.onDisconnectedCallback); } 
                catch (e) { console.warn("[JS] Callback Error:", e); }
            }
        },

        // --- Unreliable (Datagram) Reader ---
        ReadLoop: async function(reader) {
            try {
                while (true) {
                    const { value, done } = await reader.read();
                    if (done) break;

                    if (value && WT.onMessageCallback) {
                        const ptr = Module._malloc(value.length);
                        Module.HEAPU8.set(value, ptr);
                        try {
                            dynCall('vii', WT.onMessageCallback, [ptr, value.length]);
                        } finally {
                            Module._free(ptr);
                        }
                    }
                }
            } catch (e) {
                console.error("[JS] Read Loop Error:", e);
                WT.CleanupConnection("Read Error");
            }
        },

        // --- [NEW] Reliable (Stream) Listeners ---
        ListenForServerStreams: async function() {
            if (!WT.activeTransport) return;
            const streamReader = WT.activeTransport.incomingUnidirectionalStreams.getReader();

            try {
                while (true) {
                    const { value: stream, done } = await streamReader.read();
                    if (done) break;

                    // Hand off to the background worker. NO 'await' here!
                    WT.ReadSingleUniStream(stream);
                }
            } catch (e) {
                console.error("[JS] Stream listener stopped:", e);
            }
        },

        ReadSingleUniStream: async function(stream) {
            const reader = stream.getReader();
            let chunks = [];
            let totalLength = 0;

            try {
                while (true) {
                    const { value, done } = await reader.read();
                    if (done) break; // Rust called stream.finish()

                    if (value) {
                        chunks.push(value);
                        totalLength += value.length;
                    }
                }

                if (totalLength > 0 && WT.onStreamMessageCallback) {
                    // Reassemble chunks into one contiguous array
                    const completeMessage = new Uint8Array(totalLength);
                    let offset = 0;
                    for (const chunk of chunks) {
                        completeMessage.set(chunk, offset);
                        offset += chunk.length;
                    }

                    // Pass to C#
                    const ptr = Module._malloc(totalLength);
                    Module.HEAPU8.set(completeMessage, ptr);
                    try {
                        dynCall('vii', WT.onStreamMessageCallback, [ptr, totalLength]);
                    } finally {
                        Module._free(ptr);
                    }
                }
            } catch (e) {
                console.error("[JS] Error reading specific uni-stream:", e);
            }
        },

        // --- Connection Helper ---
        constructWebTransport: function(addressPtr, certificateHashPtr) {
            const address = UTF8ToString(addressPtr);

            if (certificateHashPtr) {
                const hashString = UTF8ToString(certificateHashPtr);
                
                if (!hashString || hashString.length === 0) {
                     return new WebTransport(address);
                }

                let hashBytes;
                try {
                    hashBytes = new Uint8Array(
                        hashString.trim().split(':').map(part => parseInt(part, 16))
                    );
                } catch (e) {
                    console.error('[JS] Error parsing hash:', e);
                    return null; 
                }

                return new WebTransport(address, { 
                    serverCertificateHashes: [{ algorithm: "sha-256", value: hashBytes }] 
                });
            }

            return new WebTransport(address);
        }
    },

    // -------------------------------------------------------------------------
    // EXPORTED FUNCTIONS
    // -------------------------------------------------------------------------

    WebTransport_SetCallbackOnMessageReceived__deps: ['$WT'],
    WebTransport_SetCallbackOnMessageReceived: function(callback) {
        WT.onMessageCallback = callback;
    },

    // [NEW] C# Setter for Reliable Messages
    WebTransport_SetCallbackOnStreamMessageReceived__deps: ['$WT'],
    WebTransport_SetCallbackOnStreamMessageReceived: function(callback) {
        WT.onStreamMessageCallback = callback;
    },

    WebTransport_SetCallbackOnConnected__deps: ['$WT'],
    WebTransport_SetCallbackOnConnected: function(callback) {
        WT.onOpenCallback = callback;
    },

    WebTransport_SetCallbackOnDisconnected__deps: ['$WT'],
    WebTransport_SetCallbackOnDisconnected: function(callback) {
        WT.onDisconnectedCallback = callback;
    },

    WebTransport_Connect__deps: ['$WT'],
    WebTransport_Connect: async function(addressPtr, certificateHashPtr) {
        const transport = WT.constructWebTransport(addressPtr, certificateHashPtr);

        if (!transport) {
            WT.CleanupConnection("Invalid Certificate Hash");
            return;
        }

        transport.closed
            .then(() => WT.CleanupConnection("Cleanly"))
            .catch((e) => WT.CleanupConnection(`Error: ${e}`));

        try {
            await transport.ready;
            WT.activeTransport = transport;
            
            // Unreliable writer is persistent
            WT.writer = WT.activeTransport.datagrams.writable.getWriter();
            
            console.log(`[JS] Connected. Congestion: ${WT.activeTransport.congestionControl}`);

            if (WT.onOpenCallback) dynCall('v', WT.onOpenCallback);

            // [UPDATED] Start both listening loops concurrently
            WT.ReadLoop(WT.activeTransport.datagrams.readable.getReader());
            WT.ListenForServerStreams();

        } catch (e) {
            console.error("[JS] Connection Failed:", e);
            WT.CleanupConnection("Setup Failed");
        }
    },

    // Send Unreliable
    WebTransport_Send__deps: ['$WT'],
    WebTransport_Send: async function(pointer, length) {
        if (!WT.writer) return;
        try {
            const data = Module.HEAPU8.slice(pointer, pointer + length);
            await WT.writer.write(data);
        } catch (e) {
            console.error("[JS] Send Failed:", e);
        }
    },

    // [NEW] Send Reliable
    WebTransport_SendStream__deps: ['$WT'],
    WebTransport_SendStream: async function(pointer, length) {
        if (!WT.activeTransport) return;
        try {
            // 1. Open a temporary uni-stream
            const stream = await WT.activeTransport.createUnidirectionalStream();
            const writer = stream.getWriter();
            
            // 2. Safely grab the data
            const data = Module.HEAPU8.slice(pointer, pointer + length);
            
            // 3. Send and explicitly close (this sends EOF to Rust)
            await writer.write(data);
            await writer.close(); 
        } catch (e) {
            console.error("[JS] Reliable Send Failed:", e);
        }
    },

    WebTransport_IsConnected__deps: ['$WT'],
    WebTransport_IsConnected: function() {
        return WT.activeTransport !== null && WT.writer !== null;
    },

    WebTransport_CloseConnection__deps: ['$WT'],
    WebTransport_CloseConnection: function() {
        if (WT.activeTransport) {
            WT.activeTransport.close();
        }
    }
};

mergeInto(LibraryManager.library, WTransport);