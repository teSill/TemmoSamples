using Code.Shared;
using UnityEngine;

namespace Code.Client
{   
    public class RemotePlayer : BasePlayer
    {
        private readonly LiteRingBuffer<PlayerState> _buffer = new LiteRingBuffer<PlayerState>(30);
        private float _receivedTime;
        private float _timer;
        private const float BufferTime = 0.1f; //100 milliseconds

        public RemotePlayer(ClientPlayerManager manager, string name, RemotePlayerLoginPacket remotePlayerPacket) : base(manager, name, remotePlayerPacket.InitialPlayerState.Id)
        {
            _position = remotePlayerPacket.InitialPlayerState.Position;
            _maxHealth = remotePlayerPacket.Health;
            _rotation = remotePlayerPacket.InitialPlayerState.Rotation;
            _buffer.Add(remotePlayerPacket.InitialPlayerState);
        }

        public override void Spawn(Vector2 position)
        {
            _buffer.FastClear();
            base.Spawn(position);
        }

        public void UpdatePosition(float delta)
        {
            if (_receivedTime < BufferTime || _buffer.Count < 2)
                return;
            var dataA = _buffer[0];
            var dataB = _buffer[1];
            
            float lerpTime = NetworkGeneral.SeqDiff(dataB.Tick, dataA.Tick)*LogicTimer.FixedDelta;
            float t = _timer / lerpTime;


            _position = Vector2.Lerp(dataA.Position, dataB.Position, t);
            //_rotation = Mathf.Lerp(dataA.Rotation, dataB.Rotation, t);
            _rotation = dataA.Rotation;
            _timer += delta;
            if (_timer > lerpTime)
            {
                _receivedTime -= lerpTime;
                _buffer.RemoveFromStart(1);
                _timer -= lerpTime;
            }
        }

        public void OnPlayerState(PlayerState state)
        {
            //old command
            int diff = NetworkGeneral.SeqDiff(state.Tick, _buffer.Last.Tick);
            if (diff <= 0)
                return;

            _receivedTime += diff * LogicTimer.FixedDelta;
            if (_buffer.IsFull)
            {
                Debug.LogWarning("[C] Remote: Something happened");
                //Lag?
                _receivedTime = 0f;
                _buffer.FastClear();
            }
            _buffer.Add(state);
        }

        protected override void ResetPlayerInterfaces()
        {
            
        }

        public override void SendChatMessage(string message)
        {
            
        }

        public override void ResetCombat()
        {
           
        }
    }
}