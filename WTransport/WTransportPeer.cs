using System;
using UnityEngine;

namespace WTransportFfi
{
    public class WTransportPeer
    {
        public WTransportEndPoint EndPoint;
        public uint PeerId;

        public void SendDatagram(IntPtr ptr, int length)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                WTransportBrowser.WebTransport_Send(ptr, length);
            }
            else
            {
                bool result = WTransportNative.wtransport_send_datagram(PeerId, ptr, length);
            }
        }

        public void SendStreamUni(IntPtr ptr, int length)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                WTransportBrowser.WebTransport_SendStream(ptr, length);
            }
            else
            {
                bool result = WTransportNative.wtransport_send_stream_uni(PeerId, ptr, length);
            }
        }
    }
}
