using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Shared
{
    public abstract class BasePlayer
    {
        public readonly string Name;

        private float _speed = 3f;
        private GameTimer _attackTimer = new GameTimer(0.6f);
        private GameTimer _combatTimer = new GameTimer(2.5f);
        private BasePlayerManager _playerManager;

        protected Vector2 _previousPosition;
        protected Vector2 _position;
        protected float _rotation;
        protected int _maxHealth;
        protected int _currentHealth;

        public List<NpcSpawn> RenderedNpcs { get; set; } = new List<NpcSpawn>();

        public const float Radius = 0.5f;
        public bool IsAlive => _currentHealth > 0;
        public int MaxHealth => _maxHealth;
        public int CurrentHealth => _currentHealth;

        public Dictionary<Skill, int> PlayerLevels { get; } = new Dictionary<Skill, int>();

        public Vector2 Position => _position;
        public float Rotation => _rotation;
        public Vector2 FaceDirection => _rotation == 0 ? Vector2.left : Vector2.right;
        public AttackStyle[] DefaultAttackStyles = new AttackStyle[] { AttackStyle.Offence, AttackStyle.Defence };
        public AttackStyle CurrentAttackStyle = AttackStyle.Offence;
        public readonly byte Id;
        public int Ping;

        public int CombatLevel { get; set; }
        public int CalculateCombatLevel => (int)((PlayerLevels[Skill.Offence] + PlayerLevels[Skill.Defence] + PlayerLevels[Skill.Healthpoints]) / 2.25f);
        private bool _inCombat;
        public bool InCombat 
        {
            get 
            {
                return _inCombat;
            }
            private set 
            {
                _inCombat = value;
                
                _combatTimer.Reset();
                OnCombatStateChanged?.Invoke(value);
                if (value)
                {
                    ResetPlayerInterfaces();
                } else
                {
                    ResetCombat();
                }
            }
        }

        protected abstract void ResetPlayerInterfaces();
        public abstract void ResetCombat();

        public Action<int, int> OnHealthChanged;
        public Action<bool> OnCombatStateChanged;
        public Action<int> OnCombatLevelChanged;

        protected BasePlayer(BasePlayerManager playerManager, string name, byte id)
        {
            Id = id;
            Name = name;
            _playerManager = playerManager;
        }

        public void RefreshCombatLevel(bool initialSetUp)
        {
            int currentCombatLevel = CombatLevel;
            CombatLevel = CalculateCombatLevel;

            OnCombatLevelChanged?.Invoke(CombatLevel);
            if (!initialSetUp && currentCombatLevel != CombatLevel)
            {
                SendChatMessage($"Your combat level has increased to {CombatLevel}. Congratulations!");
            }
        }

        public void InitializePlayerHealth(int maxHealth, int currentHealth)
        {
            _maxHealth = maxHealth;
            _currentHealth = currentHealth;
            OnHealthChanged?.Invoke(maxHealth, currentHealth);
        }

        public abstract void SendChatMessage(string message);

        protected void Die()
        {
            _rotation = 0;
            _currentHealth = _maxHealth;
            InCombat = false;
        }

        public virtual void Spawn(Vector2 position)
        {
            _position = position;
            Die();
        }

        public virtual void TakeDamage(byte damage)
        {
            if (_currentHealth - damage < 0)
            {
                _currentHealth = 0;
            }
            else
            {
                _currentHealth -= damage;
            }

            bool died = _currentHealth <= 0;
            if (died)
            {
                _currentHealth = _maxHealth;
            }

            OnHealthChanged?.Invoke(_maxHealth, _currentHealth);

            _playerManager.OnTakeDamage(this, damage, died);
            InCombat = true;
        }

        private void Attack()
        {
            const float MaxLength = 1f;
            //BasePlayer hitPlayer = _playerManager.CastToPlayer(_position, dir, MaxLength, this);
            //_playerManager.OnDamagePlayer(this, dir, hitPlayer);
            NpcSpawn npc = _playerManager.CastToNpc(_position, FaceDirection, MaxLength, RenderedNpcs);
            _playerManager.OnDamageNpc(this, FaceDirection, npc);
        }

        public virtual void ApplyInput(PlayerInputPacket command, float delta)
        {
            Vector2 velocity = Vector2.zero;
            
            if ((command.Keys & MovementKeys.Up) != 0)
                velocity.y = -1f;
            if ((command.Keys & MovementKeys.Down) != 0)
                velocity.y = 1f;
            
            if ((command.Keys & MovementKeys.Left) != 0)
                velocity.x = -1f;
            if ((command.Keys & MovementKeys.Right) != 0)
                velocity.x = 1f;

            _position += velocity.normalized * _speed * delta;
            _rotation = command.Rotation;

            if ((command.Keys & MovementKeys.Fire) != 0)
            {
                if (_attackTimer.IsTimeElapsed)
                {
                    _attackTimer.Reset();
                    Attack();
                }
            }
            
        }

        public virtual void Update(float delta)
        {
            _attackTimer.UpdateAsCooldown(delta);
            if (InCombat)
            {
                _combatTimer.Update(delta, () => InCombat = false);
            }
        }
    }
}

