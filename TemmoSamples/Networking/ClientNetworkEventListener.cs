using Code.Shared;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.Client
{
    public class ClientNetworkEventListener : INetEventListener
    {
        private NetPacketProcessor _packetProcessor;

        private LoginRequestPacket _cachedLoginRequestPacket;
        private LoginValidatedPacket _cachedLoginSuccessPacket;

        public Action<DisconnectInfo> OnDisconnected;

        public ClientNetworkEventListener(NetPacketProcessor netPacketProcessor)
        {
            _packetProcessor = netPacketProcessor;

            _cachedLoginRequestPacket = new LoginRequestPacket();
        }

        public NetManager CreateNetManager()
        {
            return new NetManager(this)
            {
                AutoRecycle = true,
                IPv6Enabled = false
            };
        }

        public void OnLoginRequest(string username, string password)
        {
            _cachedLoginRequestPacket.Username = username;
            _cachedLoginRequestPacket.Password = password;
        }

        public void OnLoginValidated(LoginValidatedPacket loginSuccessPacket)
        {
            _cachedLoginSuccessPacket = loginSuccessPacket;
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            if (peer.EndPoint.Port == ClientLogic.LOGIN_SERVER_PORT)
            {
                Debug.Log("Connected to login server");
                ClientPacketHandler.Instance.LoginServer = peer;
                ClientPacketHandler.Instance.SendPacket(ClientPacketHandler.Instance.LoginServer, _cachedLoginRequestPacket, DeliveryMethod.ReliableOrdered);
            } else
            {
                Debug.Log("Connected to game server");
                ClientPacketHandler.Instance.GameServer = peer;
                ClientPacketHandler.Instance.SendPacket(ClientPacketHandler.Instance.GameServer, _cachedLoginSuccessPacket, DeliveryMethod.ReliableUnordered);
            }
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {

            if (peer.EndPoint.Port == ClientLogic.LOGIN_SERVER_PORT)
            {
                Debug.Log("Disconnected from Login Server.");
                ClientPacketHandler.Instance.LoginServer = null;
                return;
            }

            OnDisconnected?.Invoke(disconnectInfo);
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Debug.Log("[C] NetworkError: " + socketError);
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            if (peer.EndPoint.Port == ClientLogic.GAME_SERVER_PORT && ClientPacketHandler.Instance.GameServer != peer)
            {
                ClientPacketHandler.Instance.GameServer = peer;
                Debug.Log("Peer to game server");
            }

            byte packetType = reader.GetByte();
            if (packetType >= NetworkGeneral.PacketTypesCount)
                return;
            PacketType pt = (PacketType)packetType;
            ClientPacketHandler.Instance.ProcessManuallySerializedPacket(pt, reader);
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
            UnconnectedMessageType messageType)
        {

        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            //_ping = latency;
        }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            request.Reject();
        }
    }
}
