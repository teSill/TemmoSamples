using Code.Shared;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Server
{
    public class ServerPlayer : BasePlayer, IPacketListener, IAttackable
    {
        private readonly ServerLogic _serverLogic;

        public readonly ServerPlayerDatabase database;
        public readonly ServerPlayerInventory inventory;
        public readonly ServerPlayerEquipment equipment;
        public readonly ServerPlayerEntityRenderer entityRenderer;
        public readonly ServerGroundItemManager groundItemManager;
        public readonly ServerPlayerSkills playerSkills;
        public readonly ServerPlayerCombat combat;

        private readonly ServerRegionManager _regionManager;

        public readonly string password;
        public bool IsNew { get; set; }
        public ServerRegion Region { get; set; }
        public List<ServerRegion> NeighbourRegions { get; set; }

        public List<GroundItemPacket> ownedGroundItems = new List<GroundItemPacket>();
            
        public readonly NetPeer AssociatedPeer;
        public PlayerState NetworkState;
        public ushort LastProcessedCommandId { get; private set; }

        public ServerNpc NpcInCombatWith { get; set; }

        public NpcDialogue PreviousDialogue { get; set; }
        public PlayerDialogue CurrentDialogue { get; set; }

        private Vector2 _respawnPosition = ServerConstants.SPAWN_POSITION;

        private PlayerChatMessagePacket _cachedChatboxMessage = new PlayerChatMessagePacket();

        public ServerPlayer(ServerPlayerManager playerManager, ServerLogic serverLogic, ServerRegionManager regionManager, ServerNpcManager npcManager, ServerGroundItemManager groundItemManager,
            string name, string password, NetPeer peer) : base(playerManager, name, (byte)peer.Id)
        {
            _serverLogic = serverLogic;
            playerSkills = new ServerPlayerSkills(this);
            database = new ServerPlayerDatabase(this, serverLogic.Database);

            ServerInventoryManager inventoryManager = new ServerInventoryManager();
            inventory = new ServerPlayerInventory(this, inventoryManager);
            equipment = new ServerPlayerEquipment(this, inventoryManager);
            
            combat = new ServerPlayerCombat(_serverLogic, this, playerSkills, equipment);

            _regionManager = regionManager;

            entityRenderer = new ServerPlayerEntityRenderer(this, npcManager);
            this.groundItemManager = groundItemManager;

            CurrentDialogue = new PlayerDialogue()
            {
                nextStageId = -1
            };

            SubscribePacketListener();

            this.password = password;
            peer.Tag = this;
            AssociatedPeer = peer;
            NetworkState = new PlayerState {Id = (byte) peer.Id};
        }

        public void SubscribePacketListener()
        {
            ServerPacketHandler.Instance.OnPacketReceived += OnReceivePlayerPacket;
        }

        private void OnReceivePlayerPacket(NetPeer peer, PacketType packetType, NetDataReader reader)
        {
            if (AssociatedPeer != peer)
            {
                return;
            }

            switch(packetType)
            {
                case PacketType.PlayerChatMessage:
                    _cachedChatboxMessage.Deserialize(reader);

                    Debug.Log($"SendChatMessage: {_cachedChatboxMessage.Player}/{Name}: {_cachedChatboxMessage.Message}");
                    ServerPacketHandler.Instance.SendSerializablePacketToRenderedPlayers(PacketType.PlayerChatMessage, _cachedChatboxMessage, DeliveryMethod.ReliableUnordered, Position);
                    break;
            }
        }

        public override void ApplyInput(PlayerInputPacket command, float delta)
        {
            if (NetworkGeneral.SeqDiff(command.Id, LastProcessedCommandId) <= 0)
                return;
            LastProcessedCommandId = command.Id;
            base.ApplyInput(command, delta);
        }

        public override void ResetCombat()
        {
            if (NpcInCombatWith != null)
            {
                NpcInCombatWith.ResetCombat();
            }
        }

        public void Respawn()
        {
            ResetCombat();
            Spawn(_respawnPosition);
            SendChatMessage("You've died!");
        }

        public void DropItemTo(int itemId, int itemAmount, Vector2 position)
        {
            DropItemPacket dropItemPacket = new DropItemPacket()
            {
                Player = Id,
                ItemId = itemId,
                ItemAmount = itemAmount,
                Position = position
            };

            if (ServerItemDatabase.Instance.ItemCollection[itemId].Stackable)
            {
                groundItemManager.PlayerDropItem(dropItemPacket, AssociatedPeer, false, itemAmount);
            }
            else
            {
                for (int i = 0; i < itemAmount; i++)
                {
                    groundItemManager.PlayerDropItem(dropItemPacket, AssociatedPeer, false, 1);
                }
            }
        }

        private void CheckCurrentRegion()
        {
            if (!_regionManager.WithinRegion(this))
            {
                Region = _regionManager.AssignRegionFromPosition(_position, this);
                Debug.Log("New region: " + Region.RegionId);
            }
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            _previousPosition = NetworkState.Position;
            NetworkState.Position = _position;

            if (_previousPosition != _position) // Player is moving
            {
                CheckCurrentRegion();
                entityRenderer.RenderGroundItems();
                entityRenderer.RenderNpcs(false);
                ResetPlayerInterfaces();
            }

            NetworkState.Rotation = _rotation;
            NetworkState.Tick = LastProcessedCommandId;
        }

        protected override void ResetPlayerInterfaces()
        {
            CurrentDialogue.nextStageId = -1;
            if (CurrentDialogue.npc != null)
            {
                CurrentDialogue.npc.EndDialogue(this);
            }
        }

        public override void SendChatMessage(string message)
        {
            ServerMessagePacket serverMessage = new ServerMessagePacket()
            {
                Message = message
            };

            AssociatedPeer.Send(ServerPacketHandler.Instance.WriteSerializable(PacketType.ServerMessage, serverMessage), DeliveryMethod.ReliableUnordered);
        }

        public Dictionary<Skill, int> GetLevels()
        {
            return PlayerLevels;
        }

        public List<Item> GetItems()
        {
            return equipment.EquippedItems;
        }
    }
}