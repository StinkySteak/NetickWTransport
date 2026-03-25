using AOT;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WTransportFfi
{
    public class WTransportNetManager
    {
        public static WTransportNetManager Instance;
        private WTransportConfig _wtransportConfig;
        private List<WTransportPeer> _connectedPeers = new();
        private Dictionary<uint, WTransportPeer> _peerByIds = new();
        private IWTransportEventListener _eventListener;
        private WTransportPeer _serverPeer;
        private string _connectAttemptUrl;
        private byte[] _receiveBuffer;
        private bool _onBrowserConnectedQueued;

        public WTransportNetManager(IWTransportEventListener listener, WTransportConfig wtransportConfig)
        {
            _eventListener = listener;
            _receiveBuffer = new byte[2048];
            _wtransportConfig = wtransportConfig;
        }

        public void Start(ushort port)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return;
            }


            if (!_wtransportConfig.EnableSsl)
            {
                string cert = WTransportNative.wtransport_start_server_dev(port);

                Debug.Log($"[{nameof(WTransportNetManager)}]: Starting server on: {port}... cert: {cert}");
            }
            else
            {
                WTransportNative.wtransport_start_server(port, _wtransportConfig.CertificatePath, _wtransportConfig.KeyPath);
                Debug.Log($"[{nameof(WTransportNetManager)}]: Starting server on: {port}...");
            }
        }

        public void Start() { }

        public void Connect(string address, int port)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            Instance = this;
            WTransportBrowser.WebTransport_SetCallbackOnConnected(OnBrowserConnectedCallback);
            WTransportBrowser.WebTransport_SetCallbackOnDisconnected(OnBrowserDisconnectedCallback);
            WTransportBrowser.WebTransport_SetCallbackOnMessageReceived(OnBrowserMessageReceivedCallback);

            string url = $"https://{address}:{port}/";
            _connectAttemptUrl = url;

            Debug.Log($"_wtransportConfig.EnableConnectUsingCertHash: {_wtransportConfig.EnableConnectUsingCertHash}");

            if (_wtransportConfig.EnableConnectUsingCertHash)
            {
                string connectServerCert = _wtransportConfig.ConnectServerCertHash;

                Debug.Log($"[{nameof(WTransportNetManager)}]: Connecting to: {url} using cert: {connectServerCert}");
                WTransportBrowser.WebTransport_Connect(url, connectServerCert);
            }
            else
            {
                Debug.Log($"[{nameof(WTransportNetManager)}]: Connecting to: {url}");
                WTransportBrowser.WebTransport_Connect(url);
            }
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnBrowserConnectedCallback()
        {
            Instance.OnBrowserConnected();
        }

        private void OnBrowserConnected()
        {
            _onBrowserConnectedQueued = true;
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnBrowserDisconnectedCallback()
        {
            Instance.OnBrowserDisconnected();
        }

        private void OnBrowserDisconnected()
        {
            _connectedPeers.Remove(_serverPeer);
            _peerByIds.Remove(_serverPeer.PeerId);
            _eventListener.OnPeerDisconnected(Instance._serverPeer, PeerDisconnectReason.Timeout);
        }

        [MonoPInvokeCallback(typeof(CallbackMessageReceived))]
        private static void OnBrowserMessageReceivedCallback(IntPtr ptr, int length)
        {
            Instance.OnBrowserMessageReceived(ptr, length);
        }

        private void OnBrowserMessageReceived(IntPtr ptr, int length)
        {
            Marshal.Copy(ptr, _receiveBuffer, 0, length);
            ArraySegment<byte> bytes = new ArraySegment<byte>(_receiveBuffer, 0, length);
            _eventListener.OnNetworkReceive(_serverPeer, bytes);
        }

        private void PollUpdateWebGL()
        {
            if (!_onBrowserConnectedQueued)
                return;

            WTransportPeer peer = new WTransportPeer();
            peer.PeerId = uint.MaxValue;
            peer.EndPoint = new WTransportEndPoint();
            peer.EndPoint.SetIPAddress(_connectAttemptUrl);

            _serverPeer = peer;
            _connectedPeers.Add(peer);
            _peerByIds.Add(peer.PeerId, peer);
            _eventListener.OnPeerConnected(peer);
            _onBrowserConnectedQueued = false;
        }

        public void PollUpdate()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                PollUpdateWebGL();
                return;
            }

            if (WTransportNative.wtransport_pop_event_client_connected(out FfiEventClientConnected evtClientConnected))
            {
                string clientAddress = WTransportNative.wtransport_get_client_address_safe(evtClientConnected.client_id);

                WTransportPeer peer = new WTransportPeer();
                peer.PeerId = evtClientConnected.client_id;
                peer.EndPoint = new WTransportEndPoint();
                peer.EndPoint.SetIPAddress(clientAddress);

                _connectedPeers.Add(peer);
                _peerByIds.Add(evtClientConnected.client_id, peer);
                _eventListener.OnPeerConnected(peer);
            }

            while (WTransportNative.wtransport_pop_event_recv_datagram(out FfiEventRecvDatagram evtDatagram))
            {
                if (_peerByIds.TryGetValue(evtDatagram.client_id, out WTransportPeer peer))
                {
                    Marshal.Copy(evtDatagram.ptr, _receiveBuffer, 0, evtDatagram.length);
                    ArraySegment<byte> bytes = new ArraySegment<byte>(_receiveBuffer, 0, evtDatagram.length);

                    WTransportNative.wtransport_free_bytes(evtDatagram.ptr, evtDatagram.length);

                    _eventListener.OnNetworkReceive(peer, bytes);
                }
            }

            if (WTransportNative.wtransport_pop_event_client_disconnected(out FfiEventClientDisconnected evtClientDisconnected))
            {
                if (!TryFindConnectedPeer(evtClientDisconnected.client_id, out int index))
                {
                    return;
                }

                WTransportPeer peer = _connectedPeers[index];
                _connectedPeers.Remove(peer);
                _peerByIds.Remove(evtClientDisconnected.client_id);
                _eventListener.OnPeerDisconnected(peer, PeerDisconnectReason.Timeout);
            }
        }

        private bool TryFindConnectedPeer(uint clientId, out int index)
        {
            for (int i = 0; i < _connectedPeers.Count; i++)
            {
                WTransportPeer peer = _connectedPeers[i];

                if (peer.PeerId == clientId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public void Stop()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                WTransportBrowser.WebTransport_CloseConnection();
            }
            else
            {
                WTransportNative.wtransport_stop_server();
            }
        }

        public void Disconnect(WTransportPeer peer)
        {
            if (peer == _serverPeer)
            {
                WTransportBrowser.WebTransport_CloseConnection();
                return;
            }

            WTransportNative.wtransport_disconnect_client(peer.PeerId);
        }
    }
}
