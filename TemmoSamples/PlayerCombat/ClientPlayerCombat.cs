using Code.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.Client {
    public class ClientPlayerCombat : BaseCombat {

        private ClientPlayer _player;

        public ClientPlayerCombat(ClientPlayer player)
        {
            _player = player;
        }
        
        public override int CalculateMaxHit(Dictionary<Skill, int> levels, List<Item> equippedItems)
        {
            return Mathf.Clamp(CalculateStrengthBonus(levels, equippedItems) / 10, 2, _hitCap);
        }

        public new int CalculateStrengthBonus(Dictionary<Skill, int> levels, List<Item> equippedItems) => base.CalculateStrengthBonus(levels, equippedItems);
        public new int CalculateAccuracyBonus(Dictionary<Skill, int> levels, List<Item> equippedItems) => base.CalculateAccuracyBonus(levels, equippedItems);
        public new int CalculateDefenceBonus(Dictionary<Skill, int> levels, List<Item> equippedItems) => base.CalculateDefenceBonus(levels, equippedItems);
    }
}
