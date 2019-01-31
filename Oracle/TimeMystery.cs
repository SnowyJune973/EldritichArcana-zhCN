// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kingmaker;
using Kingmaker.AreaLogic;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.EventConditionActionSystem.Evaluators;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Common;
using Kingmaker.UI.ServiceWindow.CharacterScreen;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Actions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using Kingmaker.Visual.Sound;
using Newtonsoft.Json;
using UnityEngine;
using static Kingmaker.RuleSystem.RulebookEvent;
using static Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class TimeMystery
    {
        internal static BlueprintProgression mystery;

        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;


        // Create the Time mystery.
        //
        // Note: Time does not have a spell list, because too many spells are missing.
        // Instead it's used with the Ancient Lorekeeper archetype.
        // (The prerequsite is added by the archetype.)
        internal static (BlueprintProgression, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            var revelations = new List<BlueprintFeature>()
            {
                CreateAgingTouch(),
                CreateEraseFromTime(),
                CreateMomentaryGlimpse(),
                CreateRewindTime(),
                CreateKnowledgeOfTheAges(),
                CreateSpeedOrSlowTime(),
                CreateTemporalClarity(),
                CreateTimeFlicker(),
                CreateTimeHop(),
                CreateTimeSight()
            };

            var skill1 = StatType.SkillAthletics;
            var skill2 = StatType.SkillMobility;
            var description = new StringBuilder(mysteryDescription).AppendLine();
            description.AppendLine(
                $"本职技能: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}\n" +
                "选择了时间秘视域的先知可以选择以下启示:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
            }

            var mystery = Helpers.CreateProgression("MysteryTimeProgression", "时间秘视域", description.ToString(),
                "b05d63ba0f634061af15c995c1a3340d",
                TimeStop.spell.Icon,
                UpdateLevelUpDeterminatorText.Group,
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat));
            mystery.Classes = oracleArray;
            TimeMystery.mystery = mystery;

            var finalRevelation = CreateFinalRevelation();
            mystery.LevelEntries = new LevelEntry[] { Helpers.LevelEntry(20, finalRevelation) };

            var revelation = Helpers.CreateFeatureSelection("MysteryTimeRevelation", "时间启示",
                mystery.Description, "d9a38bc21fd6441094e4a48de1aa4fad", null, FeatureGroup.None,
                mystery.PrerequisiteFeature());
            revelation.Mode = SelectionMode.OnlyNew;
            revelation.SetFeatures(revelations);

            return (mystery, revelation);
        }

        static BlueprintFeature CreateAgingTouch()
        {
            var name = "MysteryTimeAgingTouch";
            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "1897d4cf907249d19724c935976cef61", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var constructType = library.Get<BlueprintFeature>("fd389783027d63343b4a5634bd81645f");
            var undeadType = library.Get<BlueprintFeature>("734a29b693e9ec346ba2951b27987e33");

            var ability = Helpers.CreateAbility($"{name}Ability", "岁月之触",
                "你可以用一个近战接触攻击加速生物或是对像老化崩坏的速度。当你用此方式攻击一个活物，将造成每两先知等级1点的力量伤害。 " +
                "而对于对像或构装生物来说，此接触将造成每先知等级1d6点伤害。你每天可以使用此能力1次，之后每5级再多加1次。",
                "15b84f000f6449abb34f52c093957389",
                Helpers.GetIcon("5bf3315ce1ed4d94e8805706820ef64d"), // touch of fatigue
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Touch,
                "", "",
                Helpers.CreateDeliverTouch(),
                // configure construct damage (1d6 per level).
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.AsIs,
                    AbilityRankType.DamageDice, classes: oracleArray),
                // configure strength damage (1 per two oracle levels).
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2, min: 1, classes: oracleArray),
                Helpers.CreateRunActions(
                    Helpers.CreateConditional(Helpers.CreateConditionHasFact(constructType),
                        // Constructs: 1d6 damage per Oracle level.
                        Helpers.CreateActionDealDamage(DamageEnergyType.Magic,
                            DiceType.D6.CreateContextDiceValue(Helpers.CreateContextValueRank(AbilityRankType.DamageDice))),
                        Helpers.CreateConditional(Helpers.CreateConditionHasFact(undeadType),
                            null, // undead: no effect
                                  // living targets: 1 point of strength damage per 2 oracle levels.
                            Helpers.CreateActionDealDamage(StatType.Strength, DiceType.One.CreateContextDiceValue())))));
            ability.CanTargetEnemies = true;
            ability.EffectOnEnemy = AbilityEffectOnUnit.Harmful;

            var feat = Helpers.CreateFeature(name, ability.Name, ability.Description, "777a6cf104af4d0aaafd21707f7df3fc",
                ability.Icon, FeatureGroup.None,
                resource.CreateAddAbilityResource(),
                ability.CreateTouchSpellCast(resource).CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateEraseFromTime()
        {
            var name = "MysteryTimeErase";
            var displayName = "抹销时空";
            var description = "以一个近战接触攻击，你可以把一个生物从多元宇宙的时轴中暂时抹除。目标必须通过一个强韧检定否则彻底消失，持续轮数相当于你先知等级的一半（最少1轮）。" +
                "在这段时间里任何魔法或是神术效果都没办法找到受术者——实际上，此生物在这个时空之中存在可能性已经被暂时抹去。" +
                "在效果结束的时候，此生物将会在他之前占据的空间里面出现。如果格子已经被占据，则在旁边任何可能的格子中出现。你每天可以使用此能力1次，11级的时候额外1次。";
            var icon = Helpers.GetIcon("f001c73999fb5a543a199f890108d936"); // vanish, TODO: better icon.

            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "24ec4a772de14e65840e4848748e0d55", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 11, 1, 11, 1, 0, 0, oracleArray);

            var buff = Helpers.CreateBuff($"{name}Buff", displayName, description, "39572b0d463644bc83335850275e28a1", icon,
                null,
                UnitCondition.CantAct.CreateAddCondition(),
                UnitCondition.CantMove.CreateAddCondition(),
                UnitCondition.Invisible.CreateAddCondition(),
                Helpers.Create<Untargetable>(),
                Helpers.Create<EraseFromTimeEffect>(e => e.MakeInvisible = true));

            var ability = Helpers.CreateAbility($"{name}Ability", displayName, description,
                "b04b9fb50e8e4e71a614773ab73e1d07", icon,
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Touch,
                "", "Fortitude negates",
                Helpers.CreateDeliverTouch(),
                // Duration: 1 round per 2 oracle levels
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2, min: 1, classes: oracleArray),
                Helpers.CreateRunActions(SavingThrowType.Fortitude,
                    Helpers.CreateConditionalSaved(null,
                        Helpers.CreateApplyBuff(buff, Helpers.CreateContextDuration(),
                            fromSpell: true, dispellable: false))));
            ability.CanTargetEnemies = true;
            ability.CanTargetFriends = true;
            ability.EffectOnEnemy = AbilityEffectOnUnit.Harmful;

            var feat = Helpers.CreateFeature(name, displayName, description, "54a50c9b626a434eb56817e7503866f3",
                ability.Icon, FeatureGroup.None,
                resource.CreateAddAbilityResource(),
                ability.CreateTouchSpellCast(resource).CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateKnowledgeOfTheAges()
        {
            var feat = Helpers.CreateFeature("MysteryTimeKnowledgeOfTheAges", "岁月学识",
                "你搜寻时间，并寻找有关传奇或信息的遗漏片段。激活此技能时，你将自动重投你失败的任何知识检定，并得到相当于你魅力修正的洞察加值。每日你可以使用此能力的次数相当于你的魅力修正。",
                "4b2c7a1b5cd74658a88283c02fa0bb3e",
                Helpers.GetIcon("3adf9274a210b164cb68f472dc1e4544"), // human skilled
                FeatureGroup.None);
            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "9f085ecd82a545a09ad60749a6b8b303", null);
            resource.SetIncreasedByStat(0, StatType.Charisma);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description, "5ef4fc0b57dc4e0cb9918d1d9cc875d3",
                null, null,
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.StatBonus, ContextRankProgression.AsIs,
                    AbilityRankType.StatBonus, stat: StatType.Charisma),
                RetrySkillCheckLogic.Create(resource, Helpers.CreateContextValueRank(AbilityRankType.StatBonus),
                    StatType.SkillKnowledgeArcana, StatType.SkillKnowledgeWorld,
                    StatType.SkillLoreNature, StatType.SkillLoreReligion));
            buff.SetBuffFlags(BuffFlags.HiddenInUi);

            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "f8d5e8bc11fb4d079f40d2baacc733eb", feat.Icon, buff, AbilityActivationType.Immediately, CommandType.Free, null,
                resource.CreateActivatableResourceLogic(ResourceSpendType.Never));
            ability.IsOnByDefault = true;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateMomentaryGlimpse()
        {
            // Note: for simplicity this gives all of the +2 bonuses for 1 round, instead of to 1 roll.
            // (It's still not a great ability in combat, due to the standard action requirement.)
            var feat = Helpers.CreateFeature("MysteryTimeMomentaryGlimpse", "瞬息一瞥",
                "O每天1次，你可以看到极其有限的未来。 " +
                "在使用此能力之后1轮中，你的单次攻击检定，豁免掷骰，AC和技能检定得到+2洞察加值，持续到你下轮开始。 " +
                "5级和之后的每4级，你能使用此能力的次数增加1次。",
                "b192d167fd874508b78acdabc0850bfe",
                Helpers.GetIcon("2483a523984f44944a7cf157b21bf79c"), // elven immunities
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "6840e72ba0c14daabd08ec0b2c7ba578", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 4, 1, 0, 0, oracleArray);

            var statBonuses = new StatType[] {
                StatType.AC,
                StatType.AdditionalAttackBonus,
                StatType.SaveFortitude,
                StatType.SaveReflex,
                StatType.SaveWill,
                StatType.SkillMobility,
                StatType.SkillAthletics,
                StatType.SkillPerception,
                StatType.SkillKnowledgeArcana,
                StatType.SkillThievery,
                StatType.SkillPersuasion,
                StatType.SkillLoreNature,
                StatType.SkillStealth,
                StatType.SkillUseMagicDevice,
                StatType.SkillLoreReligion,
                StatType.SkillKnowledgeWorld,
            }.Select(s => (BlueprintComponent)s.CreateAddStatBonus(2, ModifierDescriptor.Insight)).ToArray();

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "73309cf9b3434d83859ce71f7be34e5f",
                feat.Icon, null,
                statBonuses);

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "32190b6f2b5c486cb0c9bdfa68396e09", feat.Icon,
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Personal, Helpers.oneRoundDuration, "",
                resource.CreateResourceLogic(),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(1), fromSpell: false, dispellable: false, toCaster: true)));

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateRewindTime()
        {
            // Note: reworked slightly so you get toggles to select what to reroll.
            // Because you have to anticipate the need for a reroll (which is less useful) the uses per day are doubled.
            var feat = Helpers.CreateFeature("MysteryTimeRewind", "回溯时空",
                "每日一次，作为即时动作，此能力将自动重投一个失败的d20骰。 " +
                "你可以选择希望重投的类型 (例如攻击检定，豁免检定或者技能检定等等)。 " +
                "你必须接受重投的结果，即使它比原先的结果更糟。 " +
                "你每天可以使用两次此能力。并在九级和之后的每两级获得每日一次的额外使用次数。" +
                "你必须达到7级才能选择此启示。",
                "1ae499fda64f4a2c990e04361cdf351e",
                Helpers.GetIcon("576933720c440aa4d8d42b0c54b77e80"), // evasion
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "f93c4f7c30f2451ca9602fdf3486534e", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 9, 1, 2, 1, 0, 0, oracleArray);

            var ids = new string[] {
                "759d3833b4024a9698b7e129c1c07df4",
                "e97f12a1b3144f3cadc5c757b17d4127",
                "9282c78c0fa84c8b94d6c2b1a4aca626",
                "3ddae22111ec481b8835eeddca8388ff",
                "125b5890b4024630ba2954f74a39309a",
                "d64b2ab29b5b49868bdefeee60f35406",
            };

            feat.SetComponents(resource.CreateAddAbilityResource(),
                oracle.PrerequisiteClassLevel(7),
                CreateRewindTime(feat, resource, ids[0], RuleType.AttackRoll, "攻击检定").CreateAddFact(),
                CreateRewindTime(feat, resource, ids[1], RuleType.Intiative, "先攻权检定").CreateAddFact(),
                CreateRewindTime(feat, resource, ids[2], RuleType.Maneuver, "战技检定").CreateAddFact(),
                CreateRewindTime(feat, resource, ids[3], RuleType.SavingThrow, "豁免检定").CreateAddFact(),
                CreateRewindTime(feat, resource, ids[4], RuleType.SkillCheck, "技能检定").CreateAddFact(),
                CreateRewindTime(feat, resource, ids[5], RuleType.SpellResistance, "法术抗力检定").CreateAddFact());
            return feat;
        }

        static BlueprintActivatableAbility CreateRewindTime(BlueprintFeature feat, BlueprintAbilityResource resource, String assetId, RuleType ruleType, String displayName)
        {
            var buff = Helpers.CreateBuff($"{feat.name}{ruleType}Buff", $"{feat.Name} — {displayName}", feat.Description,
                Helpers.MergeIds("a1dcc2e1a0884123a6fc85f831e033e2", assetId),
                null, null,
                Helpers.Create<ModifyD20AndSpendResource>(m =>
                {
                    m.RequiredResource = resource;
                    m.Rule = ruleType;
                    // Note: technically this should take the second roll, regardless of what it is.
                    // However if `TakeBest` is set to false, it will take the worse of the two rolls,
                    // which is not what we want.
                    m.TakeBest = true;
                    m.RollsAmount = 1;
                    m.RerollOnlyIfFailed = ruleType != RuleType.Intiative;
                }));
            buff.SetBuffFlags(BuffFlags.HiddenInUi);

            var ability = Helpers.CreateActivatableAbility($"{feat.name}{ruleType}ToggleAbility", buff.Name, feat.Description,
                assetId, feat.Icon, buff, AbilityActivationType.Immediately, CommandType.Free, null,
                resource.CreateActivatableResourceLogic(ResourceSpendType.Never));
            ability.IsOnByDefault = true;
            return ability;
        }

        static BlueprintFeature CreateSpeedOrSlowTime()
        {
            var haste = library.Get<BlueprintAbility>("486eaff58293f6441a5c2759c4872f98");
            var slow = library.Get<BlueprintAbility>("f492622e473d34747806bdb39356eb89");

            var feat = Helpers.CreateFeature("MysteryTimeSpeedOrSlow", "加减时流",
                "以一个标准动作，你可以加速或减缓时间的流动，如同“加速术”和“迟缓术”。你每天可以使用此能力1次，12和17级再加一次。你必须达到7级才能选取此启示。",
                "4df30dbc2ad147a995fce23e765b9726", haste.Icon, FeatureGroup.None);
            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "3388b2b6812246ea801c0f4140192a66", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 7, 1, 5, 1, 0, 0, oracleArray);

            var hasteAbility = DragonMystery.CopyBuffSpellToAbility(haste, $"{feat.name}加速",
                    "e996186da70d4c0b95c5a6609ff92d88", AbilityType.SpellLike, haste.Description, resource);

            var slowAbility = DragonMystery.CopyBuffSpellToAbility(slow, $"{feat.name}减速",
                    "dec33450a0b9410e90a69049afce691d", AbilityType.SpellLike, slow.Description, resource);

            feat.SetComponents(
                oracle.PrerequisiteClassLevel(7),
                resource.CreateAddAbilityResource(),
                hasteAbility.CreateAddFact(),
                slowAbility.CreateAddFact(),
                hasteAbility.CreateBindToClass(oracle, StatType.Charisma),
                slowAbility.CreateBindToClass(oracle, StatType.Charisma));
            return feat;
        }

        static BlueprintFeature CreateTimeFlicker()
        {
            // Note: reworked to use Displacement instead of Blink
            var blurBuff = library.Get<BlueprintBuff>("dd3ad347240624d46a11a092b4dd4674");
            var displacement = library.Get<BlueprintAbility>("903092f6488f9ce45a80943923576ab3");
            var displacementBuff = library.Get<BlueprintBuff>("00402bae4442a854081264e498e7a833");

            var feat = Helpers.CreateFeature("MysteryTimeFlicker", "时空闪现",
                "以一个标准动作，你在时空之间跳跃，造成如同“朦胧术”的效果。每日你可以使用此能力的分钟数相当于你的先知等级。这些时间不需要是连续的，但必须以1分钟为单位使用。7级时你可以选择造成“闪现术”的效果，不过处在闪现状态下的每轮将消耗你1分钟的使用时数。你必须达到3级才能选择此启示。",
                "76384613da7f419dbda62cf482343ef8", displacement.Icon, FeatureGroup.None);
            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "c8fba597ff6545afa4b5567a663dc4eb", null);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            var blurAbility = CreateAbilityForBuff(feat, resource, blurBuff, DurationRate.Minutes, " — " + blurBuff.Name);
            var displacementAbility = CreateAbilityForBuff(feat, resource, displacementBuff, DurationRate.Rounds, " — " + displacement.Name);

            ExclusiveAbilityToggle.AddToAbilities(blurAbility, displacementAbility);
            feat.SetComponents(
                oracle.PrerequisiteClassLevel(3),
                resource.CreateAddAbilityResource(),
                blurAbility.CreateAddFact(),
                displacementAbility.CreateAddFactOnLevelRange(oracle, 7));
            return feat;
        }

        static BlueprintActivatableAbility CreateAbilityForBuff(BlueprintFeature feat, BlueprintAbilityResource resource, BlueprintBuff buff, DurationRate duration, String extraName = "")
        {
            var abilityBuff = LingeringBuffLogic.CreateBuffForAbility(buff,
                Helpers.MergeIds(feat.AssetGuid, "9654e4eb4fbe40cba281e47de787595d"),
                resource, duration);

            var inspireCourage = library.Get<BlueprintActivatableAbility>("2ce9653d7d2e9d948aa0b03e50ae52a6");
            var ability = Helpers.CreateActivatableAbility($"{feat.name}{buff.name}ToggleAbility",
                feat.Name + extraName, feat.Description,
                Helpers.MergeIds(abilityBuff.AssetGuid, "b136041937b24d58a80a71fea93cf878"),
                buff.Icon, abilityBuff, AbilityActivationType.WithUnitCommand,
                CommandType.Standard, inspireCourage.ActivateWithUnitAnimation,
                Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));
            return ability;
        }

        static BlueprintFeature CreateTemporalClarity()
        {
            return BattleMystery.CreateRerollInitiative("MysteryTimeTemporalClarity", "时之迅捷", "5f41b06772ca43c3bcd1b6f2cdca4735");
        }

        static BlueprintFeature CreateTimeHop()
        {
            // Note: dimension door is 50 feet in game.
            // Since time hop allows 10 ft/level, that'd work out to 1 resource per 5 levels.
            // But the average jump may be shorter, so 1 per 3 seems like a good compromise.
            var dimensionDoor = library.Get<BlueprintAbility>("5bdc37e4acfa209408334326076a43bc");
            var dimensionDoorCaster = library.Get<BlueprintAbility>("a9b8be9b87865744382f7c64e599aeb2");

            var feat = Helpers.CreateFeature("MysteryTimeHop", "光阴跃步",
                "以一个移动动作，你可以像使用任意门法术一样，传送50尺。每3先知等级可使用一次。这个移动不会引起藉机攻击，你和你的目的地之间必须有效果线存在。你可以让其它自愿的生物随你伴行，不过你得消耗两次使用次数。要选择此启示，你必须达到7级。",
                "5ba08ff4d852464a9bee80442deda276", dimensionDoor.Icon, FeatureGroup.None);
            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "239347ee299945fabc0e8e18402104ab", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 3, 1, 3, 1, 0, 0, oracleArray);

            var hopMass = DragonMystery.CopyBuffSpellToAbility(dimensionDoor, $"{feat.name}MassAbility", "c92b010781a34abcb33d5b3ed6670af3",
                AbilityType.Supernatural, $"{feat.Description}\n{dimensionDoor.Description}", resource);
            hopMass.GetComponent<AbilityResourceLogic>().Amount = 2;

            var hopCaster = DragonMystery.CopyBuffSpellToAbility(dimensionDoorCaster, $"{feat.name}Ability", "cdadb4b7fb724438ac7f7f52787dad8c",
                AbilityType.Supernatural, $"{feat.Description}\n{dimensionDoorCaster.Description}", resource);

            feat.SetComponents(oracle.PrerequisiteClassLevel(7),
                resource.CreateAddAbilityResource(),
                hopCaster.CreateAddFact(), hopMass.CreateAddFact(),
                hopCaster.CreateBindToClass(oracle, StatType.Charisma),
                hopMass.CreateBindToClass(oracle, StatType.Charisma));
            return feat;
        }

        static BlueprintFeature CreateTimeSight()
        {
            var trueSeeing = library.Get<BlueprintAbility>("b3da3fbee6a751d4197e446c7e852bcb");
            var trueSeeingBuff = library.Get<BlueprintBuff>("09b4b69169304474296484c74aa12027");
            var foresight = library.Get<BlueprintAbility>("1f01a098d737ec6419aedc4e7ad61fdd");
            var foresightBuff = library.Get<BlueprintBuff>("8c385a7610aa409468f3a6c0f904ac92");

            var name = "MysteryTimeSight";
            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "6aba92b9f4b34548b485d0131433367b", null);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            var feat = Helpers.CreateFeature(name, "时间视野",
                "你穿越时间迷雾，并看透一切事物的真相，如同使用“真知术”。 " +
                //"At 15th level, this functions like moment of prescience. " +
                "18级时则可造成“预警术”的效果。每日你可以使用此能力的分钟数相当于你的先知等级。这些时间不需要是连续的。你必须达到11级才有办法选择此启示.\n" +
                $"{trueSeeing.Name}: {trueSeeing.Description}\n{foresight.Name}: {foresight.Description}",
                "a4334c2d0c094b6183a3c94bfeafae50", trueSeeing.Icon, FeatureGroup.None);

            var buff = library.CopyAndAdd(trueSeeingBuff, $"{name}Buff", "5ed3aa55269447c3837d9582a3b81b4c");
            buff.SetNameDescriptionIcon(feat);
            buff.AddComponent(Helpers.CreateAddFactContextActions(
                Helpers.CreateConditional(Helpers.Create<ContextConditionCharacterClass>(c =>
                {
                    c.CheckCaster = true;
                    c.Class = oracle;
                    c.MinLevel = 18;
                }),
                foresightBuff.CreateApplyBuff(Helpers.CreateContextDuration(0),
                    fromSpell: true, dispellable: false, asChild: true, permanent: true))));

            var ability = CreateAbilityForBuff(feat, resource, buff, DurationRate.Minutes);

            feat.SetComponents(
                oracle.PrerequisiteClassLevel(11),
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateFinalRevelation()
        {
            var timeStop = TimeStop.spell;
            var name = "MysteryTimeFinalRevelation";
            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "372c0f8d0b1241a6a3c0ef2c5b8ad459", timeStop.Icon);
            resource.SetFixedResource(1);
            var feat = Helpers.CreateFeature(name, "最终启示",
                "20级时，你终于成为时空的统驭者，同时不再衰老。你免疫任何由魔法和自然造成的衰老效果，不过之前的年纪减值仍然存在、加值也予以保留。你将永远不会老死，不过仍然可能因为意外，疾病，毒素或什么其他莫名奇妙的原因死去。此外，你每天还可以用类法术能力施展1次“时间停止”。",
                "6a3786190da74a5d9e71d83c1c327a15", timeStop.Icon, FeatureGroup.None,
                resource.CreateAddAbilityResource(),
                DragonMystery.CopyBuffSpellToAbility(timeStop, $"{name}Spell", "c85dfa2de6fb472ebc79c0802df0cefb",
                    AbilityType.SpellLike, timeStop.Description, resource).CreateAddFact());
            return feat;
        }
    }

    public class EraseFromTimeEffect : BuffLogic,
        // Can't be the target of these effects
        ITargetRulebookHandler<RuleCombatManeuver>,
        ITargetRulebookHandler<RuleAttackRoll>,
        ITargetRulebookHandler<RuleSpellTargetCheck>,
        IInitiatorRulebookHandler<RuleSavingThrow>,
        ITargetRulebookHandler<RuleDealDamage>,
        ITargetRulebookHandler<RuleDrainEnergy>,
        ITargetRulebookHandler<RuleDealStatDamage>
    {
        public bool MakeInvisible;

        [JsonProperty]
        Vector3 originalScale;

        public override void OnTurnOn()
        {
            // Increase all next event and end times for buffs (essentially freezing them for the duration)
            foreach (var buff in Owner.Buffs.Enumerable)
            {
                if (buff == Buff) continue;
                var endTime = buff.EndTime;
                if (endTime != TimeSpan.MaxValue) buff.EndTime = endTime + Buff.TimeLeft;
                var nextTick = (TimeSpan)getNextTickTime(buff);
                if (nextTick != TimeSpan.MaxValue) setNextTickTime(buff, nextTick + Buff.TimeLeft);
            }

            if (MakeInvisible)
            {
                originalScale = Owner.Unit.View.transform.localScale;
                Owner.Unit.View.transform.localScale = new Vector3(0, 0, 0);
                Log.Write($"{GetType().Name}.OnTurnOn(), save original scale {originalScale}");
            }
        }

        static readonly FastGetter getNextTickTime = Helpers.CreateGetter<Buff>("NextTickTime");
        static readonly FastSetter setNextTickTime = Helpers.CreateSetter<Buff>("NextTickTime");

        public override void OnTurnOff()
        {
            if (MakeInvisible)
            {
                Log.Write($"{GetType().Name}.OnTurnOff(), restore original scale {originalScale}");
                Owner.Unit.View.transform.localScale = originalScale;
            }
        }

        public void OnEventAboutToTrigger(RuleCombatManeuver evt)
        {
            evt.AutoFailure = true;
        }
        public void OnEventDidTrigger(RuleCombatManeuver evt) { }

        public void OnEventAboutToTrigger(RuleSavingThrow evt)
        {
            evt.AutoPass = true;
        }
        public void OnEventDidTrigger(RuleSavingThrow evt) { }

        public void OnEventAboutToTrigger(RuleSpellTargetCheck evt) { }

        public void OnEventDidTrigger(RuleSpellTargetCheck evt)
        {
            evt.IsImmune = true;
        }

        public void OnEventAboutToTrigger(RuleAttackRoll evt)
        {
            evt.AutoMiss = true;
        }
        public void OnEventDidTrigger(RuleAttackRoll evt) { }

        public void OnEventAboutToTrigger(RuleDealDamage evt)
        {
            foreach (var dmg in evt.DamageBundle)
            {
                dmg.Immune = true;
            }
        }
        public void OnEventDidTrigger(RuleDealDamage evt) { }

        public void OnEventAboutToTrigger(RuleDealStatDamage evt)
        {
            evt.Immune = true;
        }
        public void OnEventDidTrigger(RuleDealStatDamage evt) { }

        public void OnEventAboutToTrigger(RuleDrainEnergy evt)
        {
            evt.TargetIsImmune = true;
        }
        public void OnEventDidTrigger(RuleDrainEnergy evt) { }
    }

    public class RetrySkillCheckLogic : BuffLogic, IInitiatorRulebookHandler<RuleRollD20>,
        IInitiatorRulebookHandler<RuleSkillCheck>, IGlobalRulebookHandler<RulePartySkillCheck>
    {
        public ContextValue Value;
        public StatType[] Stats;
        public BlueprintAbilityResource RequiredResource;

        [JsonProperty]
        ModifiableValue.Modifier[] Modifiers;

        public static RetrySkillCheckLogic Create(BlueprintAbilityResource resource, ContextValue value, params StatType[] stats)
        {
            var r = Helpers.Create<RetrySkillCheckLogic>();
            r.RequiredResource = resource;
            r.Value = value;
            r.Stats = stats;
            return r;
        }

        public void OnEventAboutToTrigger(RuleRollD20 evt)
        {
            if (evt.Initiator != Owner.Unit) return;

            var rule = Rulebook.CurrentContext.PreviousEvent as RuleSkillCheck;
            if (rule == null || !Stats.Contains(rule.StatType)) return;

            if (!rule.IsSuccessRoll(evt.PreRollDice()))
            {
                rule.SuccessBonus = Value.Calculate(Context);
                Log.Write($"{GetType().Name} retry skill check for {rule.StatType} with bonus {rule.SuccessBonus}");
                evt.SetReroll(1, true);

                Owner.Resources.Spend(RequiredResource, 1);
                if (Owner.Resources.GetResourceAmount(RequiredResource) == 0) Buff.Remove();
            }
            else
            {
                Log.Write($"{GetType().Name} no retry needed for skill check {rule.StatType}");
            }
        }

        public void OnEventDidTrigger(RuleRollD20 evt) { }

        public void OnEventAboutToTrigger(RulePartySkillCheck evt)
        {
            var bonus = Value.Calculate(Context);
            // For party rolls, we want to take our bonus into account, so the party has the best chance of passing.
            Modifiers = Stats.Select(s => Owner.Stats.GetStat(s).AddModifier(bonus, this, ModifierDescriptor.Other)).ToArray();
        }

        public void OnEventDidTrigger(RulePartySkillCheck evt)
        {
            if (Modifiers == null) return;
            Modifiers.ForEach(m => m.Remove());
            Modifiers = null;
        }

        public void OnEventAboutToTrigger(RuleSkillCheck evt)
        {
            // If this was triggered from a party rule, remove the temporary bonuses before rolling.
            if (Modifiers == null) return;
            Modifiers.ForEach(m => m.Remove());
            Modifiers = null;
        }

        public void OnEventDidTrigger(RuleSkillCheck evt) { }
    }

    public class ModifyD20AndSpendResource : ModifyD20
    {
        public BlueprintAbilityResource RequiredResource;

        public override void OnEventAboutToTrigger(RuleRollD20 evt)
        {
            Log.Write("ModifyD20AndSpendResource: RuleRollD20 about to trigger");
            var previous = Rulebook.CurrentContext.PreviousEvent;
            if (Rule == RuleType.SavingThrow)
            {
                var rule = previous as RuleSavingThrow;
                if (rule == null) return;

                // Ensure saving throws get their stat bonus.
                var modValue = Owner.Stats.GetStat(rule.StatType);
                var statValue = (modValue as ModifiableValueAttributeStat)?.Bonus ?? modValue.ModifiedValue;
                if (!rule.IsSuccessRoll(evt.PreRollDice(), statValue - rule.StatValue))
                {
                    evt.SetReroll(1, TakeBest);
                }
            }
            else if (Rule == RuleType.SpellResistance)
            {
                var rule = previous as RuleSpellResistanceCheck;
                if (rule == null) return;

                var isSpellResisted = !rule.Initiator.Descriptor.State.Features.PrimalMagicEssence &&
                    (rule.SpellResistance > rule.SpellPenetration + evt.PreRollDice());
                if (isSpellResisted)
                {
                    evt.SetReroll(1, TakeBest);
                }
            }
            else
            {
                base.OnEventAboutToTrigger(evt);
            }
            if (evt.RerollsAmount > 0)
            {
                Log.Write($"ModifyD20AndSpendResource: reroll for {Rule}, preroll was {evt.PreRollDice()}");
                Owner.Resources.Spend(RequiredResource, 1);
                if (Owner.Resources.GetResourceAmount(RequiredResource) == 0)
                {
                    (Fact as Buff)?.Remove();
                }
            }
            else
            {
                Log.Write($"ModifyD20AndSpendResource: no reroll needed for {Rule}");
            }
        }
    }
}
