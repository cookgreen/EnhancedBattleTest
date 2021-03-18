﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnhancedBattleTest.Config;
using EnhancedBattleTest.Data.MissionData.Logic;
using EnhancedBattleTest.Multiplayer.Data.MissionData;
using EnhancedBattleTest.SinglePlayer.Data.MissionData;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.GameComponents.Map;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace EnhancedBattleTest.Data.MissionData
{
    [MissionManager]
    public static class EnhancedBattleTestMissions
    {
        //private static AtmosphereInfo CreateAtmosphereInfoForMission(string seasonString = "", int timeOfDay = 6)
        //{
        //    Dictionary<int, string> strArray = new Dictionary<int, string>
        //    {
        //        {6, "TOD_06_00_SemiCloudy"},
        //        {12, "TOD_12_00_SemiCloudy"},
        //        {15, "TOD_04_00_SemiCloudy"},
        //        {18, "TOD_03_00_SemiCloudy"},
        //        {22, "TOD_01_00_SemiCloudy"}
        //    };
        //    string str = "field_battle";
        //    strArray.TryGetValue(timeOfDay, out str);
        //    Dictionary<string, int> dictionary = new Dictionary<string, int>
        //    {
        //        {"spring", 0}, {"summer", 1}, {"fall", 2}, {"winter", 3}
        //    };
        //    dictionary.TryGetValue(seasonString, out var num);
        //    return new AtmosphereInfo
        //    {
        //        AtmosphereName = str,
        //        TimeInfo = new TimeInformation { Season = num, TimeOfDay = timeOfDay }
        //    };
        //}

        public static Mission OpenMission(BattleConfig config, string mapName)
        {
            //TODO: implement in multiplayer mode
            if (EnhancedBattleTestSubModule.IsMultiplayer)
            {
                return OpenMultiplayerMission(config, mapName);
            }

            return OpenSingleplayerMission(config, mapName);
        }

        public static Mission OpenMultiplayerMission(BattleConfig config, string map)
        {
            var playerCulture = Utility.GetCulture(config.PlayerTeamConfig);
            var playerSide = config.BattleTypeConfig.PlayerSide;

            var enemyCulture = Utility.GetCulture(config.EnemyTeamConfig);
            var enemySide = playerSide.GetOppositeSide();
            MPCombatant[] parties = new MPCombatant[2]
            {
                MPCombatant.CreateParty(playerSide, playerCulture, config.PlayerTeamConfig, true),
                MPCombatant.CreateParty(enemySide, enemyCulture, config.EnemyTeamConfig, false)
            };

            return OpenMission(parties[0], parties[1], config, map);
        }

        public static Mission OpenSingleplayerMission(BattleConfig config, string map)
        {
            var playerCulture = Utility.GetCulture(config.PlayerTeamConfig);
            var playerSide = config.BattleTypeConfig.PlayerSide;

            var enemyCulture = Utility.GetCulture(config.EnemyTeamConfig);
            var enemySide = playerSide.GetOppositeSide();
            SPCombatant[] parties = new SPCombatant[2]
            {
                SPCombatant.CreateParty(EnhancedBattleTestPartyController.PlayerParty.Party, playerSide, playerCulture,
                    config.PlayerTeamConfig, true),
                SPCombatant.CreateParty(EnhancedBattleTestPartyController.EnemyParty.Party, enemySide, enemyCulture,
                    config.EnemyTeamConfig, false)
            };
            if (playerSide == BattleSideEnum.Attacker)
                Utility.SetMapEvents(EnhancedBattleTestPartyController.PlayerParty.Party,
                    EnhancedBattleTestPartyController.EnemyParty.Party, config.BattleTypeConfig.BattleType);
            else
                Utility.SetMapEvents(EnhancedBattleTestPartyController.EnemyParty.Party,
                    EnhancedBattleTestPartyController.PlayerParty.Party, config.BattleTypeConfig.BattleType);
            return OpenMission(parties[0], parties[1], config, map);
        }

        public static Mission OpenMission(IEnhancedBattleTestCombatant playerParty,
            IEnhancedBattleTestCombatant enemyParty, BattleConfig config, string map)
        {
            if (config.BattleTypeConfig.BattleType == BattleType.Siege)
            {
                var attackerSiegeWeaponCount = GetSiegeWeaponCount(config.SiegeMachineConfig.AttackerMeleeMachines)
                    .Union(GetSiegeWeaponCount(config.SiegeMachineConfig.AttackerRangedMachines))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                var defenderSiegeWeaponCount = GetSiegeWeaponCount(config.SiegeMachineConfig.DefenderMachines);

                int breachedWallCount = config.MapConfig.BreachedWallCount;
                var hitPointPercentages = new float[2];

                switch (breachedWallCount)
                {
                    case 0:
                        hitPointPercentages[0] = 1;
                        hitPointPercentages[1] = 1;
                        break;
                    case 1:
                        int i = MBRandom.RandomInt(2);
                        hitPointPercentages[i] = 0;
                        hitPointPercentages[1 - i] = 1;
                        break;
                    default:
                        hitPointPercentages[0] = 0;
                        hitPointPercentages[1] = 0;
                        break;
                }

                return OpenEnhancedBattleTestSiege(map, config, playerParty, enemyParty, hitPointPercentages,
                    attackerSiegeWeaponCount, defenderSiegeWeaponCount, false, false, config.MapConfig.TimeOfDay);
            }

            return OpenEnhancedBattleTestField(map, config, playerParty, enemyParty, config.MapConfig.TimeOfDay);
        }


        [MissionMethod]
        public static Mission OpenEnhancedBattleTestSiege(
            string scene,
            BattleConfig config,
            IEnhancedBattleTestCombatant playerParty,
            IEnhancedBattleTestCombatant enemyParty,
            float[] wallHitPointPercentages,
            Dictionary<SiegeEngineType, int> siegeWeaponsCountOfAttackers,
            Dictionary<SiegeEngineType, int> siegeWeaponsCountOfDefenders,
            bool isSallyOut = false,
            bool isReliefForceAttack = false,
            float timeOfDay = 6f)
        {
            bool hasAnySiegeTower = siegeWeaponsCountOfAttackers.ContainsKey(DefaultSiegeEngineTypes.SiegeTower);

            string levelString;
            switch (config.MapConfig.SceneLevel)
            {
                case 1:
                    levelString = "level_1";
                    break;
                case 2:
                    levelString = "level_2";
                    break;
                case 3:
                    levelString = "level_3";
                    break;
                default:
                    levelString = "";
                    break;
            }

            var sceneLevelString =
                !(isSallyOut | isReliefForceAttack) ? levelString + " siege" : levelString + " sally";
            var playerSide = config.BattleTypeConfig.PlayerSide;
            var enemySide = config.BattleTypeConfig.PlayerSide.GetOppositeSide();


            var player = config.PlayerTeamConfig.Generals.Troops.FirstOrDefault()?.Character;
            if (player == null)
                return null;
            bool hasPlayer = config.PlayerTeamConfig.HasGeneral;
            bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
            IMissionTroopSupplier[] troopSuppliers = new IMissionTroopSupplier[2];
            bool isMultiplayer = EnhancedBattleTestSubModule.IsMultiplayer;
            troopSuppliers[(int)playerSide] = CreateTroopSupplier(playerParty, isMultiplayer);
            troopSuppliers[(int)enemySide] = CreateTroopSupplier(enemyParty, isMultiplayer);
            bool isPlayerGeneral = config.BattleTypeConfig.PlayerType == PlayerType.Commander || !hasPlayer;
            bool isPlayerSergeant = hasPlayer && config.BattleTypeConfig.PlayerType == PlayerType.Sergeant;

            List<CharacterObject> charactersInPlayerSideByPriority = null;
            List<CharacterObject> charactersInEnemySideByPriority = null;
            string playerTeamGeneralName = null;
            string enemyTeamGeneralName = null;
            if (!isMultiplayer)
            {
                var playerCharacter = player.CharacterObject as CharacterObject;
                if (playerCharacter == null)
                    return null;
                charactersInPlayerSideByPriority = Utility.OrderHeroesByPriority(config.PlayerTeamConfig);
                var playerGeneral = hasPlayer && isPlayerGeneral
                    ? playerCharacter
                    : (hasPlayer && charactersInPlayerSideByPriority.First() == playerCharacter
                        ? charactersInPlayerSideByPriority.Skip(1).FirstOrDefault()
                        : charactersInPlayerSideByPriority.FirstOrDefault());
                if (playerGeneral != null)
                {
                    charactersInPlayerSideByPriority.Remove(playerGeneral);
                    playerTeamGeneralName = TextObject.ConvertToStringList(new List<TextObject>
                    {
                        playerGeneral.Name
                    }).FirstOrDefault();
                }
                charactersInEnemySideByPriority = Utility.OrderHeroesByPriority(config.EnemyTeamConfig);
                var enemyGeneral = charactersInEnemySideByPriority.FirstOrDefault();
                if (enemyGeneral != null)
                {
                    charactersInEnemySideByPriority.Remove(enemyGeneral);
                    enemyTeamGeneralName = TextObject.ConvertToStringList(new List<TextObject>
                    {
                        enemyGeneral.Name
                    }).FirstOrDefault();

                }
            }

            AtmosphereInfo atmosphereInfo = AtmosphereModel.CreateAtmosphereInfoForMission(config.MapConfig.Season, timeOfDay);

            var attackerSiegeWeapons =
                GetSiegeWeaponTypes(siegeWeaponsCountOfAttackers);
            var defenderSiegeWeapons =
                GetSiegeWeaponTypes(siegeWeaponsCountOfDefenders);
            return MissionState.OpenNew("EnhancedBattleTestSiegeBattle", new MissionInitializerRecord(scene)
            {
                PlayingInCampaignMode = false,
                AtmosphereOnCampaign = atmosphereInfo,
                SceneLevels = sceneLevelString,
                TimeOfDay = timeOfDay
            }, mission =>
            {
                List<MissionBehaviour> missionBehaviourList = new List<MissionBehaviour>
                {
                    new CommanderLogic(config),
                    new BattleSpawnLogic(isSallyOut
                        ? "sally_out_set"
                        : (isReliefForceAttack ? "relief_force_attack_set" : "battle_set")),
                    new MissionOptionsComponent(),
                    new BattleEndLogic(),
                    new MissionCombatantsLogic(null, playerParty,
                        !isPlayerAttacker ? playerParty : enemyParty,
                        isPlayerAttacker ? playerParty : enemyParty,
                        !isSallyOut ? Mission.MissionTeamAITypeEnum.Siege : Mission.MissionTeamAITypeEnum.SallyOut,
                        isPlayerSergeant),
                    new SiegeMissionPreparationHandler(isSallyOut, isReliefForceAttack, wallHitPointPercentages,
                        hasAnySiegeTower),
                    new MissionAgentSpawnLogic(troopSuppliers, playerSide),
                    new BattleObserverMissionLogic(),
                    new CustomBattleAgentLogic(),
                    new AgentBattleAILogic(),
                    new AmmoSupplyLogic(new List<BattleSideEnum> {BattleSideEnum.Defender}),
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new SiegeMissionController(
                        attackerSiegeWeapons,
                        defenderSiegeWeapons,
                        isPlayerAttacker, isSallyOut),
                    new SiegeDeploymentHandler(isPlayerAttacker,
                        isPlayerAttacker ? attackerSiegeWeapons : defenderSiegeWeapons),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(),
                    new AgentMoraleInteractionLogic(),
                    new HighlightsController(),
                    new BattleHighlightsController(),
                    new AssignPlayerRoleInTeamMissionController(isPlayerGeneral, isPlayerSergeant, hasPlayer, charactersInPlayerSideByPriority?.Select(character => character.StringId).ToList()),
                    new CreateBodyguardMissionBehavior(
                        isPlayerAttacker ? playerTeamGeneralName : enemyTeamGeneralName,
                        !isPlayerAttacker ? playerTeamGeneralName : enemyTeamGeneralName,
                        null, null, false),
                };
                if (isSallyOut)
                {
                    missionBehaviourList.Add(new EnhancedBattleTestSiegeSallyOutMissionSpawnHandler(
                        isPlayerAttacker ? enemyParty : playerParty,
                        isPlayerAttacker ? playerParty : enemyParty));
                }
                else
                {
                    missionBehaviourList.Add(new EnhancedBattleTestSiegeMissionSpawnHandler(
                        isPlayerAttacker ? enemyParty : playerParty,
                        isPlayerAttacker ? playerParty : enemyParty));
                    missionBehaviourList.Add(new AgentFadeOutLogic());
                }
                return missionBehaviourList;
            });
        }


        [MissionMethod]
        public static Mission OpenEnhancedBattleTestField(
            string scene,
            BattleConfig config,
            IEnhancedBattleTestCombatant playerParty,
            IEnhancedBattleTestCombatant enemyParty,
            float timeOfDay = 12f)
        {
            var playerSide = config.BattleTypeConfig.PlayerSide;
            var enemySide = config.BattleTypeConfig.PlayerSide.GetOppositeSide();
            var player = config.PlayerTeamConfig.Generals;
            if (player == null)
                return null;

            bool hasPlayer = config.PlayerTeamConfig.HasGeneral;

            bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
            IMissionTroopSupplier[] troopSuppliers = new IMissionTroopSupplier[2];
            bool isMultiplayer = EnhancedBattleTestSubModule.IsMultiplayer;
            troopSuppliers[(int)playerSide] = CreateTroopSupplier(playerParty, isMultiplayer);
            troopSuppliers[(int)enemySide] = CreateTroopSupplier(enemyParty, isMultiplayer);
            bool isPlayerGeneral = config.BattleTypeConfig.PlayerType == PlayerType.Commander;
            bool isPlayerSergeant = hasPlayer && config.BattleTypeConfig.PlayerType == PlayerType.Sergeant;

            List<CharacterObject> charactersInPlayerSideByPriority = null;
            List<CharacterObject> charactersInEnemySideByPriority = null;
            string playerTeamGeneralName = null;
            string enemyTeamGeneralName = null;
            if (!isMultiplayer)
            {
                var playerCharacter = config.PlayerTeamConfig.Generals.Troops.FirstOrDefault()?.Character.CharacterObject as CharacterObject;
                if (playerCharacter == null)
                    return null;
                charactersInPlayerSideByPriority = Utility.OrderHeroesByPriority(config.PlayerTeamConfig);
                var playerGeneral = hasPlayer && isPlayerGeneral
                    ? playerCharacter
                    : (hasPlayer && charactersInPlayerSideByPriority.First() == playerCharacter
                        ? charactersInPlayerSideByPriority.Skip(1).FirstOrDefault()
                        : charactersInPlayerSideByPriority.FirstOrDefault());
                if (playerGeneral != null)
                {
                    charactersInPlayerSideByPriority.Remove(playerGeneral);
                    playerTeamGeneralName = TextObject.ConvertToStringList(new List<TextObject>
                    {
                        playerGeneral.Name
                    }).FirstOrDefault();
                }
                charactersInEnemySideByPriority = Utility.OrderHeroesByPriority(config.EnemyTeamConfig);
                var enemyGeneral = charactersInEnemySideByPriority.FirstOrDefault();
                if (enemyGeneral != null)
                {
                    charactersInEnemySideByPriority.Remove(enemyGeneral);
                    enemyTeamGeneralName = TextObject.ConvertToStringList(new List<TextObject>
                    {
                        enemyGeneral.Name
                    }).FirstOrDefault();
                }
            }


            AtmosphereInfo atmosphereInfo = AtmosphereModel.CreateAtmosphereInfoForMission(config.MapConfig.Season, timeOfDay);
            return MissionState.OpenNew("EnhancedBattleTestFieldBattle", new MissionInitializerRecord(scene)
            {
                DoNotUseLoadingScreen = false,
                PlayingInCampaignMode = false,
                AtmosphereOnCampaign = atmosphereInfo,
                TimeOfDay = timeOfDay
            }, mission =>
                new MissionBehaviour[]
                {
                    new CommanderLogic(config),
                    new MissionOptionsComponent(),
                    new BattleEndLogic(),
                    new MissionCombatantsLogic(null, playerParty,
                        !isPlayerAttacker ? playerParty : enemyParty,
                        isPlayerAttacker ? playerParty : enemyParty,
                        Mission.MissionTeamAITypeEnum.FieldBattle, isPlayerSergeant),
                    new MissionDefaultCaptainAssignmentLogic(),
                    new BattleObserverMissionLogic(),
                    new CustomBattleAgentLogic(),
                    new MissionAgentSpawnLogic(troopSuppliers, playerSide),
                    new EnhancedBattleTestMissionSpawnHandler(!isPlayerAttacker ? playerParty : enemyParty,
                        isPlayerAttacker ? playerParty : enemyParty),
                    new AgentBattleAILogic(),
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(),
                    new BattleMissionAgentInteractionLogic(),
                    new FieldBattleController(),
                    new AgentFadeOutLogic(),
                    new AgentMoraleInteractionLogic(),
                    new AssignPlayerRoleInTeamMissionController(isPlayerGeneral, isPlayerSergeant, hasPlayer,
                        charactersInPlayerSideByPriority?.Select(character => character.StringId).ToList()),
                    new CreateBodyguardMissionBehavior(
                        isPlayerAttacker ? playerTeamGeneralName : enemyTeamGeneralName,
                        !isPlayerAttacker ? playerTeamGeneralName : enemyTeamGeneralName),
                    new HighlightsController(),
                    new BattleHighlightsController()
                });
        }

        private static IEnhancedBattleTestTroopSupplier CreateTroopSupplier(IEnhancedBattleTestCombatant combatant, bool isMultiplayer)
        {
            if (isMultiplayer)
            {
                return new MPTroopSupplier(combatant);
            }

            return new SPTroopSupplier(combatant);
        }

        private static Dictionary<SiegeEngineType, int> GetSiegeWeaponCount(List<string> siegeWeaponIds)
        {
            Dictionary<SiegeEngineType, int> siegeWeaponsCount = new Dictionary<SiegeEngineType, int>();
            foreach (var attackerMeleeMachine in siegeWeaponIds)
            {
                var siegeWeapon = Utility.GetSiegeEngineType(attackerMeleeMachine);
                if (siegeWeapon == null)
                    continue;
                if (!siegeWeaponsCount.ContainsKey(siegeWeapon))
                    siegeWeaponsCount.Add(siegeWeapon, 0);
                siegeWeaponsCount[siegeWeapon]++;
            }

            return siegeWeaponsCount;
        }
        private static Type GetSiegeWeaponType(SiegeEngineType siegeWeaponType)
        {
            if (siegeWeaponType == DefaultSiegeEngineTypes.Ladder)
                return typeof(SiegeLadder);
            if (siegeWeaponType == DefaultSiegeEngineTypes.Ballista)
                return typeof(Ballista);
            if (siegeWeaponType == DefaultSiegeEngineTypes.FireBallista)
                return typeof(FireBallista);
            if (siegeWeaponType == DefaultSiegeEngineTypes.Ram || siegeWeaponType == DefaultSiegeEngineTypes.ImprovedRam)
                return typeof(BatteringRam);
            if (siegeWeaponType == DefaultSiegeEngineTypes.SiegeTower)
                return typeof(SiegeTower);
            if (siegeWeaponType == DefaultSiegeEngineTypes.Onager || siegeWeaponType == DefaultSiegeEngineTypes.Catapult)
                return typeof(Mangonel);
            if (siegeWeaponType == DefaultSiegeEngineTypes.FireOnager || siegeWeaponType == DefaultSiegeEngineTypes.FireCatapult)
                return typeof(FireMangonel);
            return siegeWeaponType == DefaultSiegeEngineTypes.Trebuchet || siegeWeaponType == DefaultSiegeEngineTypes.Bricole ? typeof(Trebuchet) : null;
        }

        private static Dictionary<Type, int> GetSiegeWeaponTypes(
            Dictionary<SiegeEngineType, int> values)
        {
            Dictionary<Type, int> dictionary = new Dictionary<Type, int>();
            foreach (KeyValuePair<SiegeEngineType, int> keyValuePair in values)
                dictionary.Add(GetSiegeWeaponType(keyValuePair.Key), keyValuePair.Value);
            return dictionary;
        }
    }
}
