using Code.Shared;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Server {
    public class ServerPlayerCombat : BaseCombat {

        private ServerLogic _serverLogic;
        private ServerPlayer _player;
        private ServerPlayerSkills _playerSkills;
        private ServerPlayerEquipment _playerEquipment;
        private ServerPlayerAttackStyles _attackStyles;

        private readonly float _maxAccuracyRolls = 100;

        public ServerPlayerCombat(ServerLogic serverLogic, ServerPlayer player, ServerPlayerSkills playerSkills, ServerPlayerEquipment playerEquipment)
        {
            _serverLogic = serverLogic;
            _player = player;
            _playerSkills = playerSkills;
            _playerEquipment = playerEquipment;

            _attackStyles = new ServerPlayerAttackStyles(_player, _playerEquipment);
        }

        public void AttackPlayer(Vector2 target, BasePlayer otherPlayer)
        {
            SendAttackPacket(_player, target);
            if (otherPlayer != null)
            {
                // TODO: Apply damage formula to players
                //byte damage = (byte)CalculateHitDamage(npc);
                byte damage = 0;
                AddExperience(damage);
                _player.TakeDamage(damage);
                PlayerTakeDamagePacket damagePacket = new PlayerTakeDamagePacket
                {
                    PlayerDoingDamage = _player.Id,
                    PlayerTakingDamage = otherPlayer.Id,
                    Damage = damage
                };

                ServerPacketHandler.Instance.SendSerializablePacketToRenderedPlayers(PacketType.PlayerTakeDamage, damagePacket, DeliveryMethod.ReliableUnordered, _player.Position);
            }
        }

        public void AttackNpc(Vector2 target, NpcSpawn npcSpawn)
        {
            SendAttackPacket(_player, target);
            ServerNpc npc = (ServerNpc)npcSpawn;

            if (npc == null)
                return;
            
            if (_player.InCombat && _player.NpcInCombatWith != npc)
            {
                _player.SendChatMessage("You're already fighting something else!");
                return;
            }

            if (npc.PlayerInCombatWith != null && npc.PlayerInCombatWith != _player)
            {
                _player.SendChatMessage( $"NPC[{npc.Name}] is already in combat.");
                return;
            }

            if (npc != null && npc.IsAttackable(_player))
            {
                byte damage = (byte) CalculateHitDamage(_player, npc);
                AddExperience(damage);
                npc.TakeDamage(damage, _player);
                if (!npc.Alive)
                {
                    return;
                }

                NpcTakeDamagePacket damagePacket = new NpcTakeDamagePacket
                {
                    PlayerDoingDamage = _player.Id,
                    NpcSpawnId = npc.SpawnId,
                    Damage = damage,
                    CurrentHealth = npc.CurrentHealth
                };

                ServerPacketHandler.Instance.SendSerializablePacketToRenderedPlayers(PacketType.NpcTakeDamage, damagePacket, DeliveryMethod.ReliableUnordered, npc.CurrentServerPosition);
            }
        }

        public override int CalculateMaxHit(Dictionary<Skill, int> levels, List<Item> equippedItems)
        {
            return Mathf.Clamp(CalculateStrengthBonus(levels, equippedItems) / 10, 2, _hitCap);
        }

        private void AddExperience(byte damage)
        {
            _playerSkills.AddExperience((Skill)Enum.Parse(typeof(Skill), _player.CurrentAttackStyle.ToString()), damage * ServerSkillManager.COMBAT_EXPERIENCE_PER_HIT);
            _playerSkills.AddExperience(Skill.Healthpoints, damage * ServerSkillManager.HP_EXPERIENCE_PER_HIT);
        }

        private void SendAttackPacket(ServerPlayer player, Vector2 target)
        {
            AttackPacket attackPacket = new AttackPacket
            {
                FromPlayer = player.Id,
                CommandId = player.LastProcessedCommandId,
                ServerTick = _serverLogic.Tick,
                Hit = target
            };

            ServerPacketHandler.Instance.SendSerializablePacketToRenderedPlayers(PacketType.RemotePlayerAttack, attackPacket, DeliveryMethod.ReliableUnordered, player.Position);
        }
    }
}
