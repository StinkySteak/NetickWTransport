const WTransport = {
    // -------------------------------------------------------------------------
    // INTERNAL STATE & LOGIC ($WT)
    // -------------------------------------------------------------------------
    $WT: {
        activeTransport: null,
        writer: null,
        onMessageCallback: null,
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

        // [FIXED] Renamed arguments and fixed logic
        constructWebTransport: function(addressPtr, certificateHashPtr) {
            const address = UTF8ToString(addressPtr);

            // [FIX 1] Check the pointer, not the undefined variable
            if (certificateHashPtr) {
                const hashString = UTF8ToString(certificateHashPtr);
                
                // If string is empty, treat as normal connection
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
                    return null; // Signals failure
                }

                return new WebTransport(address, { 
                    serverCertificateHashes: [{ algorithm: "sha-256", value: hashBytes }] 
                });
            }

            // No Cert provided
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

    WebTransport_SetCallbackOnConnected__deps: ['$WT'],
    WebTransport_SetCallbackOnConnected: function(callback) {
        WT.onOpenCallback = callback;
    },

    WebTransport_SetCallbackOnDisconnected__deps: ['$WT'],
    WebTransport_SetCallbackOnDisconnected: function(callback) {
        WT.onDisconnectedCallback = callback;
    },

    // [FIXED] Now uses WT.constructWebTransport
    WebTransport_Connect__deps: ['$WT'],
    WebTransport_Connect: async function(addressPtr, certificateHashPtr) {
        // [FIX 2] Call the helper via the internal object 'WT'
        const transport = WT.constructWebTransport(addressPtr, certificateHashPtr);

        // [FIX 3] Check for null (hash parsing failure)
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
            WT.writer = WT.activeTransport.datagrams.writable.getWriter();
            
            console.log(`[JS] Connected. Congestion: ${WT.activeTransport.congestionControl}`);

            if (WT.onOpenCallback) dynCall('v', WT.onOpenCallback);

            await WT.ReadLoop(WT.activeTransport.datagrams.readable.getReader());

        } catch (e) {
            console.error("[JS] Connection Failed:", e);
            WT.CleanupConnection("Setup Failed");
        }
    },

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