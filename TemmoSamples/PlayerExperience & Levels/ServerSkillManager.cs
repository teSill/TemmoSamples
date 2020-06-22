using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Code.Server {
    public class ServerSkillManager {

        private const int FIRST_LEVEL_EXPERIENCE = 30;
        public const int MAX_SKILL_LEVEL = 120;
        public const float MAX_EXPERIENCE = 1000000000;

        public const int COMBAT_EXPERIENCE_PER_HIT = 5;
        public const int HP_EXPERIENCE_PER_HIT = 2;

        public Dictionary<int, int> LevelExperienceRequirements { get; private set; } = new Dictionary<int, int>();

        public static ServerSkillManager Instance { get; private set; }

        public ServerSkillManager()
        {
            Instance = this;
            SetLevelExperienceRequirements();
        }

        private void SetLevelExperienceRequirements()
        {
            int previousExperience = 0;
            int currentRequiredExperienceForLevel = FIRST_LEVEL_EXPERIENCE;
            for (int i = 1; i <= MAX_SKILL_LEVEL; i++)
            {
                LevelExperienceRequirements.Add(i, currentRequiredExperienceForLevel);
                if (i == 1)
                {
                    //Debug.Log("Level: " + i + ". Experience required: " + currentRequiredExperienceForLevel);
                    previousExperience = currentRequiredExperienceForLevel;
                    continue;
                }
                
                currentRequiredExperienceForLevel = (int) ((previousExperience + (i * 15)) * 1.065f);
                previousExperience = currentRequiredExperienceForLevel;
                //Debug.Log("Level: " + i + ". Experience required: " + currentRequiredExperienceForLevel);
            }
        }

        public double GetLevelProgressPercentage(int currentLevel, float currentExperience)
        {
            if (currentLevel == MAX_SKILL_LEVEL)
                return 1;

            float currentLevelExpRequirement = LevelExperienceRequirements.ElementAt(currentLevel).Value;
            float nextLevelExpRequirement = LevelExperienceRequirements.ElementAt(currentLevel + 1).Value;

            float difference = nextLevelExpRequirement - currentLevelExpRequirement;

            return (double)(currentExperience - currentLevelExpRequirement) / difference;
        }

        public int GetLevelForExperience(float experience)
        {
            if (experience < FIRST_LEVEL_EXPERIENCE)
                return 1;

            for(int i = LevelExperienceRequirements.Count - 1; i > 0; i--)
            {
                if (experience >= LevelExperienceRequirements.ElementAt(i).Value)
                {
                    return LevelExperienceRequirements.ElementAt(i - 1).Key;
                }
            }

            return MAX_SKILL_LEVEL;
        }

        public float GetExperienceForLevel(int level)
        {
            for(int i = 0; i < LevelExperienceRequirements.Count; i++)
            {
                if (LevelExperienceRequirements.ElementAt(i).Key >= level)
                {
                    return LevelExperienceRequirements.ElementAt(i + 1).Value;
                }
            }

            Debug.Log("Returned 0 experience for level");
            return 0;
        }

        public float GetNextLevelExpRequirement(int currentLevel)
        {
            return LevelExperienceRequirements.ElementAt(currentLevel + 1).Value;
        }

    }
}
