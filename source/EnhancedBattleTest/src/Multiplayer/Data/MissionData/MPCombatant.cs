﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnhancedBattleTest.Config;
using EnhancedBattleTest.Data.MissionData;
using EnhancedBattleTest.Multiplayer.Config;
using TaleWorlds.Core;

namespace EnhancedBattleTest.Multiplayer.Data.MissionData
{
    public class MPCombatant : EnhancedBattleTestCombatant
    {
        private readonly List<MPSpawnableCharacter> _characters = new List<MPSpawnableCharacter>();
        private int _tacticLevel;
        public IEnumerable<MPSpawnableCharacter> MPCharacters => _characters.AsReadOnly();
        public override IEnumerable<BasicCharacterObject> Characters => MPCharacters.Select(character => character.Character);

        public override int NumberOfHealthyMembers => _characters.Count;

        public MPCombatant(BattleSideEnum side, int tacticLevel, BasicCultureObject culture,
            Tuple<uint, uint> primaryColorPair, Tuple<uint, uint> alternativeColorPair, Banner banner)
            : base(GameTexts.FindText("str_ebt_side", side == BattleSideEnum.Attacker ? "Attacker" : "Defender"),
                side, culture, primaryColorPair, alternativeColorPair, banner)
        {
            _tacticLevel = tacticLevel;
        }

        public MPCombatant(BattleSideEnum side, int tacticLevel, BasicCultureObject culture, Tuple<uint, uint> primaryColorPair, Banner banner)
            : base(GameTexts.FindText("str_ebt_side", side == BattleSideEnum.Attacker ? "Attacker" : "Defender"),
                side, culture, primaryColorPair, new Tuple<uint, uint>(primaryColorPair.Item2, primaryColorPair.Item1), banner)
        {
            _tacticLevel = tacticLevel;
        }

        public static MPCombatant CreateParty(BattleSideEnum side, BasicCultureObject culture,
            TeamConfig teamConfig, bool isPlayerTeam)
        {
            bool isAttacker = side == BattleSideEnum.Attacker;
            uint color1 = Utility.BackgroundColor(culture, isAttacker);
            uint color2 = Utility.ForegroundColor(culture, isAttacker);
            var combatant = new MPCombatant(side, teamConfig.TacticLevel, culture,
                new Tuple<uint, uint>(color1, color2), new Banner(culture.BannerKey, color1, color2));
            if (teamConfig.HasGeneral)
            {
                if (teamConfig.General is MPCharacterConfig general)
                    combatant.AddCharacter(
                        new MPSpawnableCharacter(general, (int)general.CharacterObject.DefaultFormationClass,
                            general.FemaleRatio > 0.5, isPlayerTeam), 1);
            }
            for (int i = 0; i < teamConfig.Troops.Troops.Length; ++i)
            {
                var troopConfig = teamConfig.Troops.Troops[i];
                var mpCharacter = troopConfig.Character as MPCharacterConfig;
                if (mpCharacter == null)
                    continue;
                var femaleCount = (int)(troopConfig.Number * mpCharacter.FemaleRatio + 0.49);
                var maleCount = troopConfig.Number - femaleCount;
                combatant.AddCharacter(new MPSpawnableCharacter(mpCharacter, i, true),
                    femaleCount);
                combatant.AddCharacter(new MPSpawnableCharacter(mpCharacter, i, false), maleCount);
            }

            return combatant;
        }

        public override int GetTacticsSkillAmount()
        {
            return _tacticLevel;
        }

        public void AddCharacter(MPSpawnableCharacter character, int number)
        {
            for (int index = 0; index < number; ++index)
                this._characters.Add(character);
            this.NumberOfAllMembers += number;
        }

        public void KillCharacter(MPSpawnableCharacter character)
        {
            this._characters.Remove(character);
        }
    }
}
