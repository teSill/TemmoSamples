using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Shared {
    public abstract class BaseCombat {

        protected readonly int _hitCap = 100;

        public abstract int CalculateMaxHit(Dictionary<Skill, int> levels, List<Item> equippedItems);

        protected virtual int CalculateAccuracyBonus(Dictionary<Skill, int> levels, List<Item> equippedItems)
        {
            float accuracyBonus = 0;

            accuracyBonus += levels[Skill.Offence] * 1.05f;
            if (equippedItems != null)
            {
                accuracyBonus += GetItemBonusFromEquippedItems(equippedItems, ItemBonusType.Accuracy);
            }

            return (int)accuracyBonus;
        }

        protected virtual int CalculateStrengthBonus(Dictionary<Skill, int> levels, List<Item> equippedItems)
        {
            float strengthBonus = 0;

            strengthBonus += levels[Skill.Offence] * 1.625f;
            if (equippedItems != null)
            {
                strengthBonus += GetItemBonusFromEquippedItems(equippedItems, ItemBonusType.Strength);
            }

            return (int)strengthBonus;
        }

        protected virtual int CalculateDefenceBonus(Dictionary<Skill, int> levels, List<Item> equippedItems)
        {
            float defenceBonus = 0;

            defenceBonus += levels[Skill.Defence] * 1.35f;
            if (equippedItems != null)
            {
                defenceBonus += GetItemBonusFromEquippedItems(equippedItems, ItemBonusType.Defence);
            }

            return (int)defenceBonus;
        }

        public virtual int CalculateHitDamage(IAttackable attacker, IAttackable opponent)
        {
            Dictionary<Skill, int> attackerLevels = attacker.GetLevels();
            Dictionary<Skill, int> opponentLevels = opponent.GetLevels();
            List<Item> attackerEquippedItems = attacker.GetItems();
            List<Item> opponentEquippedItems = opponent.GetItems();

            float enemyDefence = opponentLevels[Skill.Defence];
            float baseAccuracy = CalculateAccuracyBonus(attackerLevels, attackerEquippedItems);
            float subtractedAccuracy = baseAccuracy - enemyDefence;
            int maxHit = CalculateMaxHit(attackerLevels, attackerEquippedItems);

            int chanceOfHitting = 55 + (int)subtractedAccuracy;
            int rndNum = Random.Range(0, chanceOfHitting);

            int hit = GetHitFromHitType(GetHitType(rndNum), maxHit);
            Debug.Log("Attacker accuracy: " + baseAccuracy + ". Attacker strength: " + CalculateStrengthBonus(attackerLevels, attackerEquippedItems) + ". Opponent defence: " + enemyDefence);
            Debug.Log($"Base chance of hitting {chanceOfHitting}. Roll: {rndNum}. Damage: " + hit + ". Max hit: " + maxHit);

            return hit;
        }

        protected virtual HitType GetHitType(int roll)
        {
            switch (roll)
            {
                case int n when roll >= 85:
                    return HitType.ExtremeDamage;
                case int n when roll >= 70:
                    return HitType.HighDamage;
                case int n when roll >= 50:
                    return HitType.MediumDamage;
                case int n when roll >= 20:
                    return HitType.LowDamage;
                default:
                    return HitType.Miss;

            }
        }

        protected virtual int GetHitFromHitType(HitType hitType, int maxHit)
        {
            switch (hitType)
            {
                case HitType.ExtremeDamage:
                    return (int)Mathf.Ceil(Mathf.Clamp(Random.Range(maxHit * 0.65f, maxHit), 1, maxHit));
                case HitType.HighDamage:
                    return (int)Mathf.Ceil(Mathf.Clamp(Random.Range(maxHit * 0.50f, maxHit), 1, maxHit));
                case HitType.MediumDamage:
                    return (int)Mathf.Ceil(Mathf.Clamp(Random.Range(maxHit * 0.30f, maxHit), 1, maxHit));
                case HitType.LowDamage:
                    return (int)Mathf.Ceil(Mathf.Clamp(Random.Range(maxHit * 0.15f, maxHit), 1, maxHit));
                default:
                    return 0;
            }
        }

        public int GetItemBonusFromEquippedItems(List<Item> equippedItems, ItemBonusType bonus)
        {
            float itemBonuses = 0;
            foreach (Item item in equippedItems)
            {
                switch (bonus)
                {
                    case ItemBonusType.Accuracy:
                        itemBonuses += item.AccuracyBonus;
                        break;
                    case ItemBonusType.Strength:
                        itemBonuses += item.StrengthBonus;
                        break;
                    case ItemBonusType.Defence:
                        itemBonuses += item.DefenceBonus;
                        break;
                }
            }
            Debug.Log("Returning " + itemBonuses + " for BonusType: " + bonus);
            return (int)itemBonuses;
        }

        protected enum HitType {
            Miss,
            LowDamage,
            MediumDamage,
            HighDamage,
            ExtremeDamage
        }
    }

    public enum ItemBonusType {
        Accuracy,
        Strength,
        Defence
    }
}
