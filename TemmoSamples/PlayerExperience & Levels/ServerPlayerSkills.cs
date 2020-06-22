using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Shared;
using LiteNetLib;
using System.Linq;

namespace Code.Server {

    public class ServerPlayerSkills {

        private ServerPlayer _player;

        public Dictionary<Skill, float> PlayerExperiences { get; } = new Dictionary<Skill, float>();

        private readonly float _startingHpExperience = 1093;

        public ServerPlayerSkills(ServerPlayer player)
        {
            _player = player;
        }

        public void InitializeExperience()
        {
            foreach (Skill skill in Enum.GetValues(typeof(Skill)))
            {
                int startingLevel = skill == Skill.Healthpoints ? ServerSkillManager.Instance.GetLevelForExperience(_startingHpExperience) : 1;
                float startingExperience = skill == Skill.Healthpoints ? _startingHpExperience : 0;

                _player.PlayerLevels.Add(skill, startingLevel);
                PlayerExperiences.Add(skill, startingExperience);

                SendExperiencePacket(skill, startingExperience, true);
            }

            _player.InitializePlayerHealth(_player.PlayerLevels[Skill.Healthpoints], _player.PlayerLevels[Skill.Healthpoints]);
            _player.CombatLevel = _player.CalculateCombatLevel;
        }

        public void LoadExperience(float[] loadedExperience)
        {
            for(int i = 0; i < loadedExperience.Length; i++)
            {
                _player.PlayerLevels.Add((Skill)i, ServerSkillManager.Instance.GetLevelForExperience(loadedExperience[i]));
                PlayerExperiences.Add((Skill)i, loadedExperience[i]);

                SendExperiencePacket((Skill)i, loadedExperience[i], true);
            }

            _player.InitializePlayerHealth(_player.PlayerLevels[Skill.Healthpoints], _player.PlayerLevels[Skill.Healthpoints]);
            _player.CombatLevel = _player.CalculateCombatLevel;
        }

        public void AddExperience(Skill skill, float experience)
        {
            if (experience <= 0)
                return;

            PlayerExperiences[skill] += experience;

            if (PlayerExperiences[skill] >= ServerSkillManager.MAX_EXPERIENCE)
            {
                PlayerExperiences[skill] = ServerSkillManager.MAX_EXPERIENCE;
                return;
            }

            if (_player.PlayerLevels[skill] >= ServerSkillManager.MAX_SKILL_LEVEL)
            {
                return;
            }

            double levelProgress = ServerSkillManager.Instance.GetLevelProgressPercentage(_player.PlayerLevels[skill], PlayerExperiences[skill]);
            if (levelProgress >= 1)
            {
                _player.PlayerLevels[skill]++;
                levelProgress = ServerSkillManager.Instance.GetLevelProgressPercentage(_player.PlayerLevels[skill], PlayerExperiences[skill]);

                if (skill == Skill.Healthpoints)
                {
                    _player.InitializePlayerHealth(_player.PlayerLevels[Skill.Healthpoints], _player.CurrentHealth + 1);
                }

                LevelUpPacket levelUpPacket = new LevelUpPacket()
                {
                    Skill = (int)skill,
                    Level = _player.PlayerLevels[skill]
                };

                _player.AssociatedPeer.Send(ServerPacketHandler.Instance.WriteSerializable(PacketType.LevelUpPacket, levelUpPacket), DeliveryMethod.ReliableUnordered);

                _player.CombatLevel = _player.CalculateCombatLevel;
            }

            SendExperiencePacket(skill, experience, false);
        }

        private void SendExperiencePacket(Skill skill, float experience, bool initialLoad)
        {
            ExperiencePacket experiencePacket = new ExperiencePacket()
            {
                Skill = (byte)skill,
                Level = _player.PlayerLevels[skill],
                Experience = experience,
                LevelProgress = ServerSkillManager.Instance.GetLevelProgressPercentage(_player.PlayerLevels[skill], PlayerExperiences[skill]),
                CurrentTotalExperience = PlayerExperiences[skill],
                NextLevelExpRequirement = ServerSkillManager.Instance.GetNextLevelExpRequirement(_player.PlayerLevels[skill]),
                Initial = initialLoad
            };

            _player.AssociatedPeer.Send(ServerPacketHandler.Instance.WriteSerializable(PacketType.ExperienceGain, experiencePacket), DeliveryMethod.ReliableUnordered);
        }

        public int GetSkillLevel(Skill skill)
        {
            return _player.PlayerLevels[skill];
        }

        public int GetTotalExperience()
        {
            float experience = 0;
            foreach(var skill in PlayerExperiences)
            {
                experience += skill.Value;
            }

            return (int) experience;
        }
    }
}
