using Netick.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using WTransportFfi;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = nameof(WTransportProvider), menuName = "Netick/Transport/WTransportProvider")]
    public class WTransportProvider : NetworkTransportProvider
    {
        public WTransportConfig WTransportConfig;

        public override NetworkTransport MakeTransportInstance()
        {
            return new WTransport(this);
        }
    }

    public class WTransport : NetworkTransport, IWTransportEventListener
    {
        private WTransportProvider _transportProvider;
        private WTransportNetManager _netManager;
        private Dictionary<WTransportPeer, WTransportConnection> _connections;
        private Queue<WTransportConnection> _freeConnections;
        private BitBuffer _buffer;
        private byte[] _receiveBuffer;

        public WTransport(WTransportProvider transportProvider)
        {
            _transportProvider = transportProvider;
        }

        public override void Init()
        {
            _netManager = new WTransportNetManager(this, _transportProvider.WTransportConfig);
            _connections = new(Engine.MaxClients);
            _freeConnections = new(Engine.MaxClients);
            _buffer = new BitBuffer(createChunks: false);

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
            {
                _freeConnections.Enqueue(new WTransportConnection());
            }

            _receiveBuffer = new byte[2048];
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            _netManager.Connect(address, port);
        }

        public override void Disconnect(TransportConnection connection)
        {
            _netManager.Disconnect(((WTransportConnection)connection).Peer);
        }

        public override void PollEvents()
        {
            _netManager.PollUpdate();
        }

        public override void ForceUpdate()
        {
            _netManager.PollUpdate();
        }

        public override void Run(RunMode mode, int port)
        {
            if (mode == RunMode.Server)
            {
                _netManager.Start((ushort)port);
            }
            else if (mode == RunMode.Client)
            {
                _netManager.Start();
            }
        }

        public override void Shutdown()
        {
            _netManager.Stop();
        }

        void IWTransportEventListener.OnPeerConnected(WTransportPeer peer)
        {
            WTransportConnection connection = _freeConnections.Dequeue();
            connection.Peer = peer;

            _connections.Add(peer, connection);
            NetworkPeer.OnConnected(connection);
        }

        unsafe void IWTransportEventListener.OnNetworkReceive(WTransportPeer peer, ArraySegment<byte> bytes)
        {
            if (!_connections.TryGetValue(peer, out WTransportConnection connection))
            {
                return;
            }

            Array.Copy(bytes.Array, 0, _receiveBuffer, 0, bytes.Count);

            fixed (byte* ptr = _receiveBuffer)
            {
                _buffer.SetFrom(ptr, bytes.Count, bytes.Count);

                NetworkPeer.Receive(connection, _buffer);
            }
        }

        void IWTransportEventListener.OnPeerDisconnected(WTransportPeer peer, PeerDisconnectReason disconnectReason)
        {
            if (Engine.IsClient)
            {
                if (peer == null)
                {
                    NetworkPeer.OnConnectFailed(ConnectionFailedReason.Timeout);
                    return;
                }

                NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
            }

            if (peer == null)
                return;

            if (_connections.TryGetValue(peer, out WTransportConnection connection))
            {
                NetworkPeer.OnDisconnected(connection, TransportDisconnectReason.Timeout);
                _connections.Remove(peer);
                _freeConnections.Enqueue(connection);
            }
        }
    }

    public class WTransportConnection : TransportConnection
    {
        public WTransportPeer Peer;

        public override IEndPoint EndPoint => Peer.EndPoint;

        public override int Mtu => 1200;

        public override void Send(IntPtr ptr, int length)
        {
            Peer.SendDatagram(ptr, length);
        }

        public override void SendUserData(IntPtr ptr, int length, TransportDeliveryMethod transportDeliveryMethod)
        {
            if (transportDeliveryMethod == TransportDeliveryMethod.Unreliable)
            {
                Peer.SendDatagram(ptr, length);
            }
            else if (transportDeliveryMethod == TransportDeliveryMethod.Reliable)
            {
                Peer.SendStreamUni(ptr, length);
            }
        }
    }
}
