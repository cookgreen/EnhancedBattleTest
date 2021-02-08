﻿using System.Collections.Generic;
using TaleWorlds.Core;

namespace EnhancedBattleTest.Data.MissionData
{
    public interface IEnhancedBattleTestCombatant : IBattleCombatant
    {
        int NumberOfAllMembers { get; }
        int NumberOfHealthyMembers { get; }

        IEnumerable<BasicCharacterObject> Characters { get; }
    }
}
