using System;

namespace WTransportFfi
{
    public interface IWTransportEventListener
    {
        void OnPeerConnected(WTransportPeer peer);
        void OnNetworkReceive(WTransportPeer peer, ArraySegment<byte> bytes);
        void OnPeerDisconnected(WTransportPeer peer, PeerDisconnectReason disconnectReason);
    }
}
