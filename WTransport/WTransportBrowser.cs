using System.Runtime.InteropServices;
using System;

namespace WTransportFfi
{
    public static class WTransportBrowser
    {
        private const string DllName = "__Internal";

#if UNITY_WEBGL
        [DllImport("__Internal")]
        internal static extern void WebTransport_Connect(string address, string cert = null);

        [DllImport("__Internal")]
        internal static extern void WebTransport_CloseConnection();

        [DllImport("__Internal")]
        internal static extern void WebTransport_Send(IntPtr ptr, int length);
        
        [DllImport("__Internal")]
        internal static extern void WebTransport_SendStream(IntPtr ptr, int length);

        [DllImport("__Internal")]
        internal static extern void WebTransport_SetCallbackOnConnected(Action callback);

        [DllImport("__Internal")]
        internal static extern void WebTransport_SetCallbackOnDisconnected(Action callback);

        [DllImport("__Internal")]
        internal static extern void WebTransport_SetCallbackOnMessageReceived(CallbackMessageReceived callback);

        [DllImport("__Internal")]
        public static extern void WebTransport_SetCallbackOnStreamMessageReceived(CallbackMessageReceived callback);
#else
        internal static void WebTransport_Connect(string address, string cert = null) { }
        internal static void WebTransport_CloseConnection() { }
        internal static void WebTransport_SetCallbackOnConnected(Action callback) { }
        internal static void WebTransport_SetCallbackOnMessageReceived(CallbackMessageReceived callback) { }
        internal static void WebTransport_SetCallbackOnStreamMessageReceived(CallbackMessageReceived callback) { }
        internal static void WebTransport_Send(IntPtr ptr, int length) { }
        internal static void WebTransport_SendStream(IntPtr ptr, int length) { }
        internal static void WebTransport_SetCallbackOnDisconnected(Action callback) { }
#endif
    }

    public delegate void CallbackMessageReceived(IntPtr ptr, int length);
}
