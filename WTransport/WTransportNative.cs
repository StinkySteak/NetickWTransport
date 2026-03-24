using System;
using System.Runtime.InteropServices;

namespace WTransportFfi
{
    public static class WTransportNative
    {
        private const string DllName = "wtransport_ffi";

        [DllImport(DllName)]
        public static extern IntPtr wtransport_start_server(ushort port);

        [DllImport(DllName)]
        public static extern bool wtransport_send_datagram(uint peerId, IntPtr ptr, int length);

        [DllImport(DllName)]
        public static extern bool wtransport_free_string(IntPtr ptr);

        [DllImport(DllName)]
        public static extern void wtransport_stop_server();

        [DllImport(DllName)]
        public static extern bool wtransport_pop_event_client_connected(out FfiEventClientConnected evt);

        [DllImport(DllName)]
        public static extern bool wtransport_pop_event_client_disconnected(out FfiEventClientDisconnected evt);

        [DllImport(DllName)]
        public static extern bool wtransport_pop_event_recv_datagram(out FfiEventRecvDatagram evt);

        [DllImport(DllName)]
        private static extern IntPtr wtransport_get_client_address(uint clientId);

        [DllImport(DllName)]
        public static extern void wtransport_disconnect_client(uint clientId);
        
        [DllImport(DllName)]
        public static extern void wtransport_free_bytes(IntPtr ptr, int length);

        public static string wtransport_get_client_address_safe(uint clientId)
        {
            IntPtr ptr = wtransport_get_client_address(clientId);

            if (ptr == IntPtr.Zero)
            {
                return "Unknown";
            }

            string address = Marshal.PtrToStringAnsi(ptr);
            wtransport_free_string(ptr);

            return address;
        }

        public static string wtransport_start_server_random_cert(ushort port)
        {
            IntPtr ptr = wtransport_start_server(port);

            string hash = Marshal.PtrToStringAnsi(ptr);
            wtransport_free_string(ptr);
            return hash;
        }
    }
}
