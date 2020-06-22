using Code.Shared;
using Unity;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Code.Client {
    public class ClientPacketHandler {
        private ClientLogic _clientLogic;
        private NetPacketProcessor _packetProcessor;
        private ClientPlayerManager _playerManager;
        private NetDataWriter _writer;

        public NetPeer LoginServer { get; set; }
        public NetPeer GameServer { get; set; }

        private LoginValidatedPacket _cachedLoginSuccessPacket;

        public Action<LoginValidatedPacket> OnLoginServerValidated;
        public Action<LoginAcceptPacket, string> OnLoggedIn;
        public Action<RemotePlayerLoginPacket> OnRemoteConnection;

        public Action<PacketType, NetPacketReader> OnPacketReceived;

        public Action<Item, int, int> OnReceivedInventoryItem;
        public Action<ClientPlayer, Item, int, bool> OnReceivedEquippedItem;

        public static ClientPacketHandler Instance { get; private set; }

        public ClientPacketHandler(ClientLogic clientLogic, NetPacketProcessor packetProcessor, ClientPlayerManager playerManager)
        {
            Instance = this;

            _writer = new NetDataWriter();
            _clientLogic = clientLogic;
            _packetProcessor = packetProcessor;
            _playerManager = playerManager;

            SetListeners();
        }

        private void SetListeners()
        {
            _packetProcessor.RegisterNestedType((w, v) => w.Put(v), reader => reader.GetVector2());
            _packetProcessor.RegisterNestedType<PlayerState>();
            _packetProcessor.RegisterNestedType<GroundItemPacket>();
            _packetProcessor.RegisterNestedType<ServerItemPacket>();
            _packetProcessor.RegisterNestedType<NpcSpawnPacket>();
            _packetProcessor.RegisterNestedType<DialogueOptionPacket>();

            _packetProcessor.SubscribeReusable<LoginValidatedPacket>(OnLoginValidated);
            _packetProcessor.SubscribeReusable<RemotePlayerLoginPacket>(OnRemotePlayerConnected);
            _packetProcessor.SubscribeReusable<LoginAcceptPacket>(OnLoginAccepted);
            _packetProcessor.SubscribeReusable<PlayerDisconnectedPacket>(OnPlayerLeft);
            _packetProcessor.SubscribeReusable<PlayerInitialDataPacket>(OnReceiveItemData);;
        }

        public void SendPacketSerializable<T>(PacketType type, T packet, DeliveryMethod deliveryMethod) where T : INetSerializable
        {
            if (GameServer == null)
                return;
            _writer.Reset();
            _writer.Put((byte)type);
            packet.Serialize(_writer);
            GameServer.Send(_writer, deliveryMethod);
        }

        public void SendPacket<T>(NetPeer server, T packet, DeliveryMethod deliveryMethod) where T : class, new()
        {
            if (server == null)
                return;
            _writer.Reset();
            _writer.Put((byte)PacketType.Serialized);
            _packetProcessor.Write(_writer, packet);
            server.Send(_writer, deliveryMethod);
            Debug.Log("Packet sent to " + server.EndPoint);
        }

        public void ConnectToLoginServer(string username, string password)
        {
            if (LoginServer == null)
            {
                _clientLogic.Connect(ClientLogic.IP, ClientLogic.LOGIN_SERVER_PORT);
            }
            else
            {
                SendPacket(LoginServer, new LoginRequestPacket() { Username = username, Password = password }, DeliveryMethod.ReliableOrdered);
            }
        }

        public void ProcessManuallySerializedPacket(PacketType packetType, NetPacketReader reader)
        {
            if (packetType == PacketType.Serialized)
            {
                _packetProcessor.ReadAllPackets(reader);
                return;
            }

            OnPacketReceived?.Invoke(packetType, reader);
            
        }

        private void OnLoginValidated(LoginValidatedPacket loginSuccessPacket)
        {
            Debug.Log("Login request has been validated on the Login Server: " + loginSuccessPacket.SessionKey);
            LoginServer.Disconnect();
            _cachedLoginSuccessPacket = loginSuccessPacket;
            OnLoginServerValidated?.Invoke(loginSuccessPacket);
            _clientLogic.Connect(ClientLogic.IP, ClientLogic.GAME_SERVER_PORT);
        }

        private void OnLoginAccepted(LoginAcceptPacket packet)
        {
            Debug.Log("Login verified on the game server. Received player ID: " + packet.Id);
            OnLoggedIn?.Invoke(packet, _cachedLoginSuccessPacket.Username);
        }

        private void OnRemotePlayerConnected(RemotePlayerLoginPacket packet)
        {
            Debug.Log($"Player connected: {packet.Username}");
            OnRemoteConnection?.Invoke(packet);
        }

        private void OnPlayerLeft(PlayerDisconnectedPacket packet)
        {
            var player = _playerManager.RemovePlayer(packet.Id);
            if (player != null)
            {
                Debug.Log($"Player disconnected: {player.Name}");
            }
        }

        // TODO: Move out of this class
        private void OnReceiveItemData(PlayerInitialDataPacket itemDatabasePacket)
        {
            int[] itemIds = itemDatabasePacket.InventoryItemIds;
            int[] itemAmounts = itemDatabasePacket.InventoryItemAmounts;

            // Inventory
            for (int i = 0; i < itemDatabasePacket.InventoryItemIds.Length; i++)
            {
                OnReceivedInventoryItem?.Invoke(ClientItemDatabase.Instance.GetItemById(itemIds[i]), i, itemAmounts[i]);
            }

            // Equipment
            foreach (int itemId in itemDatabasePacket.EquippedItems)
            {
                OnReceivedEquippedItem?.Invoke(_playerManager.OurPlayer, ClientItemDatabase.Instance.GetItemById(itemId), -1, true);
            }

            ClientPlayerView clientPlayerView = (ClientPlayerView)_playerManager.GetViewById(_playerManager.OurPlayer.Id);
            clientPlayerView.Player.InitializePlayerHealth(itemDatabasePacket.MaxHealth, itemDatabasePacket.CurrentHealth);
        }
    }
}
