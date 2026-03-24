using System;
using System.Runtime.InteropServices;

namespace WTransportFfi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FfiEventClientConnected
    {
        public uint client_id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FfiEventClientDisconnected
    {
        public uint client_id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FfiEventRecvDatagram
    {
        public uint client_id;
        public IntPtr ptr;
        public int length;
    }
}
