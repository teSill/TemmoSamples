using Code.Shared;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using UnityEngine;

namespace Code.Client
{
    public class ClientPlayer : BasePlayer, IPacketListener
    {
        private ClientLogic _clientLogic;
        private PlayerInputPacket _nextCommand;
        public readonly ClientPlayerManager playerManager;
        private readonly LiteRingBuffer<PlayerInputPacket> _predictionPlayerStates;
        private ServerState _lastServerState;
        private const int MaxStoredCommands = 60;
        private bool _firstStateReceived;
        private int _updateCount;

        public ClientPlayerCombat PlayerCombat { get; }

        private PlayerTakeDamagePacket _cachedPlayerDamagePacket = new PlayerTakeDamagePacket();
        private LevelUpPacket _cachedLevelUpPacket = new LevelUpPacket();

        public Vector2 LastPosition { get; private set; }
        public float LastRotation { get; private set; }

        public bool IsTyping;

        public int StoredCommands => _predictionPlayerStates.Count;

        public static string PlayerName { get; private set; }

        public ClientPlayer(ClientLogic clientLogic, ClientPlayerManager manager, string name, byte id) : base(manager, name, id)
        {
            _clientLogic = clientLogic;
            playerManager = manager;
            _predictionPlayerStates = new LiteRingBuffer<PlayerInputPacket>(MaxStoredCommands);
            PlayerName = name;
            PlayerCombat = new ClientPlayerCombat(this);

            SubscribePacketListener();
        }

        public void SubscribePacketListener()
        {
            ClientPacketHandler.Instance.OnPacketReceived += OnReceivePlayerPacket;
        }

        private void OnReceivePlayerPacket(PacketType packetType, NetDataReader reader)
        {
            switch(packetType)
            {
                case PacketType.PlayerTakeDamage:
                    _cachedPlayerDamagePacket.Deserialize(reader);
                    TakeDamage(_cachedPlayerDamagePacket.Damage);
                    break;
                case PacketType.LevelUpPacket:
                    _cachedLevelUpPacket.Deserialize(reader);
                    SendChatMessage($"Congratulations! You've advanced to level {_cachedLevelUpPacket.Level} in {((Skill)_cachedLevelUpPacket.Skill).ToString()}.");
                    if (_cachedLevelUpPacket.Skill == (int)Skill.Healthpoints)
                    {
                        _maxHealth = _cachedLevelUpPacket.Level;
                        _currentHealth++;
                        OnHealthChanged?.Invoke(_maxHealth, _currentHealth);
                    }
                    break;
            }
        }

        public void ReceiveServerState(ServerState serverState, PlayerState ourState)
        {
            if (!_firstStateReceived)
            {
                if (serverState.LastProcessedCommand == 0)
                    return;
                _firstStateReceived = true;
            }
            if (serverState.Tick == _lastServerState.Tick || 
                serverState.LastProcessedCommand == _lastServerState.LastProcessedCommand)
                return;

            _lastServerState = serverState;

            //sync
            _position = ourState.Position;
            _rotation = ourState.Rotation;
            if (_predictionPlayerStates.Count == 0)
                return;

            ushort lastProcessedCommand = serverState.LastProcessedCommand;
            int diff = NetworkGeneral.SeqDiff(lastProcessedCommand,_predictionPlayerStates.First.Id);
            
            //apply prediction
            if (diff >= 0 && diff < _predictionPlayerStates.Count)
            {
                //Debug.Log($"[OK]  SP: {serverState.LastProcessedCommand}, OUR: {_predictionPlayerStates.First.Id}, DF:{diff}");
                _predictionPlayerStates.RemoveFromStart(diff+1);
                foreach (var state in _predictionPlayerStates)
                    ApplyInput(state, LogicTimer.FixedDelta);
            }
            else if(diff >= _predictionPlayerStates.Count)
            {
                Debug.Log($"[C] Player input lag st: {_predictionPlayerStates.First.Id} ls:{lastProcessedCommand} df:{diff}");
                //lag
                _predictionPlayerStates.FastClear();
                _nextCommand.Id = lastProcessedCommand;
            }
            else
            {
                Debug.Log($"[ERR] SP: {serverState.LastProcessedCommand}, OUR: {_predictionPlayerStates.First.Id}, DF:{diff}, STORED: {StoredCommands}");
            }
        }

        public override void ResetCombat()
        {
            
        }

        public override void TakeDamage(byte damage)
        {
            base.TakeDamage(damage);
            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        public void SendLootItem(Item item, int itemAmount, Vector2 position)
        {
            GameAudioManager.Instance.PlaySoundEffect(GameSound.PickUpItem);
            ClientPlayerView view = (ClientPlayerView) playerManager.GetViewById(Id);

            ClientPacketHandler.Instance.SendPacketSerializable(PacketType.PickUpItem,
                new PickUpItemPacket
                {
                    Player = Id,
                    ItemId = item.Id,
                    ItemAmount = itemAmount,
                    Position = position
                },
                DeliveryMethod.ReliableUnordered);
        }

        public void SendDropItem(Item item, int slot)
        {
            GameAudioManager.Instance.PlaySoundEffect(GameSound.DropItem);
            DropItemPacket itemDrop = new DropItemPacket()
            {
                Player = Id,
                ItemId = item.Id,
                ItemInventorySlot = slot,
                Position = Position
            };

            ClientPacketHandler.Instance.SendPacketSerializable(PacketType.DropItem, itemDrop, DeliveryMethod.ReliableUnordered);
        }

        public override void SendChatMessage(string message) => ((ClientPlayerView) playerManager.GetViewById(Id)).DisplayChatMessage(message);

        public void SendChatMessagePacket(BasePlayer originalPlayer, string message)
        {
            ClientPacketHandler.Instance.SendPacketSerializable(PacketType.PlayerChatMessage,
                new PlayerChatMessagePacket
                {
                    Player = originalPlayer.Id,
                    Message = message
                },
                DeliveryMethod.Unreliable);
        }

        protected override void ResetPlayerInterfaces()
        {
        
        }

        public override void Spawn(Vector2 position)
        {
            base.Spawn(position);
        }

        public void SetInput(Vector2 velocity, float rotation, bool fire)
        {
            if (IsTyping)
            {
                return;
            }

            _nextCommand.Keys = 0;
            if (fire)
            {
                _nextCommand.Keys |= MovementKeys.Fire;
            }
            
            if (velocity.x < -0.5f)
                _nextCommand.Keys |= MovementKeys.Left;
            if (velocity.x > 0.5f)
                _nextCommand.Keys |= MovementKeys.Right;
            if (velocity.y < -0.5f)
                _nextCommand.Keys |= MovementKeys.Up;
            if (velocity.y > 0.5f)
                _nextCommand.Keys |= MovementKeys.Down;

            if (_nextCommand.Keys != 0)
            {
                ResetPlayerInterfaces();
            }

            _nextCommand.Rotation = rotation;
        }

        public override void Update(float delta)
        {
            LastPosition = _position;
            LastRotation = _rotation;

            _nextCommand.Id = (ushort)((_nextCommand.Id + 1) % NetworkGeneral.MaxGameSequence);
            _nextCommand.ServerTick = _lastServerState.Tick;
            ApplyInput(_nextCommand, delta);
            if (_predictionPlayerStates.IsFull)
            {
                _nextCommand.Id = (ushort)(_lastServerState.LastProcessedCommand+1);
                _predictionPlayerStates.FastClear();
            }
            _predictionPlayerStates.Add(_nextCommand);

            _updateCount++;
            if (_updateCount == 3)
            {
                _updateCount = 0;
                foreach (PlayerInputPacket playerInputPacket in _predictionPlayerStates)
                    ClientPacketHandler.Instance.SendPacketSerializable(PacketType.Movement, playerInputPacket, DeliveryMethod.Unreliable);
            }

            base.Update(delta);
        }
    }
}