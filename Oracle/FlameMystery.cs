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
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
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
using Kingmaker.UnitLogic.Mechanics.Properties;
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
    static class FlameMystery
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static (BlueprintFeature, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            // Note: Gaze of Flames removed, as it wouldn't do anything in PF:K
            var revelations = new List<BlueprintFeature>()
            {
                CreateBurningMagic(),
                CreateFirestorm(),
                CreateCinderDance(),
                CreateHeatAura(),
                CreateFireBreath(),
                CreateFormOfFlame(),
                CreateMoltenSkin(),
                CreateTouchOfFlame(),
                CreateWingsOfFire()
            };

            var skill1 = StatType.SkillAthletics;
            var skill2 = StatType.SkillMobility;
            var description = new StringBuilder(mysteryDescription).AppendLine();
            description.AppendLine(
                $"本职技能: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}\n" +
                "选择了火焰秘视域的先知可以选择以下启示:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
            }

            var mystery = Helpers.CreateProgression("MysteryFlameProgression", "火焰秘视域", description.ToString(),
                "11205333c1be4c6f868e413633fc7557",
                Helpers.GetIcon("17cc794d47408bc4986c55265475c06f"), // Fire elemental bloodline
                UpdateLevelUpDeterminatorText.Group,
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat));
            mystery.Classes = oracleArray;

            var spells = Bloodlines.CreateSpellProgression(mystery, new String[] {
                "4783c3709a74a794dbe7c8e7e0b1b038", // burning hands
                "21ffef7791ce73f468b6fca4d9371e8b", // resist energy
                "2d81362af43aeac4387a3d4fced489c3", // fireball
                FireSpells.wallOfFire.AssetGuid,  // wall of fire 
                "ebade19998e1f8542a1b55bd4da766b3", // fire snake (should be: summon elemental, moved to 6th level) 
                "4814f8645d1d77447a70479c0be51c72", // summon elemental huge fire (should be: fire seeds)
                "e3d0dfe1c8527934294f241e0ae96a8d", // fire storm
                FireSpells.incendiaryCloud.AssetGuid,
                "08ccad78cac525040919d51963f9ac39", // fiery body
            });

            var entries = new List<LevelEntry>();
            for (int level = 1; level <= 9; level++)
            {
                entries.Add(Helpers.LevelEntry(level * 2, spells[level - 1]));
            }
            var finalRevelation = CreateFinalRevelation();
            entries.Add(Helpers.LevelEntry(20, finalRevelation));

            mystery.LevelEntries = entries.ToArray();
            mystery.UIGroups = Helpers.CreateUIGroups(new List<BlueprintFeatureBase>(spells) { finalRevelation });

            var revelation = Helpers.CreateFeatureSelection("MysteryFlameRevelation", "Flame Revelation",
                mystery.Description, "40db1e0f9b3a4f5fb9fde0801b158216", null, FeatureGroup.None,
                mystery.PrerequisiteFeature());
            revelation.Mode = SelectionMode.OnlyNew;
            revelation.SetFeatures(revelations);
            return (mystery, revelation);
        }

        static BlueprintFeature CreateBurningMagic()
        {
            var feat = Helpers.CreateFeature("MysteryFlameBurningMagic", "燃尽魔法",
                "如果某个目标没有通过你释放的火系魔法的豁免检定并受到火焰伤害，他将着火。在轮到着火的目标开始行动的时候他受到每法术等级1点的火焰伤害。着火状态将持续1d4轮，但可以用一个移动动作结束这一状态，只要该目标通过一个反射检定（使用那个法术的DC）。将目标浸湿水可以使他在豁免检定上获得+2加值，而把他浸入水中则可以直接将火熄灭。不需要通过豁免的法术不会使目标着火。",
                "ae7b2ef7026d4de494779f6112f8dfba",
                Helpers.GetIcon("42a65895ba0cb3a42b6019039dd2bff1"), // molten orb
                FeatureGroup.None);

            var burningBuff = library.CopyAndAdd<BlueprintBuff>("ef7d021abb6bbfd4cad4f2f2b70bcf28", // FirestormBuff
                $"{feat.name}Buff", "5066021721b043a08c847fd277ec715f");
            burningBuff.SetNameDescriptionIcon(feat.Name, feat.Description, feat.Icon);
            burningBuff.SetComponents(
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.CustomProperty, ContextRankProgression.AsIs,
                    AbilityRankType.DamageBonus,
                    customProperty: SpellLevelPropertyGetter.Blueprint.Value),
                Helpers.CreateAddFactContextActions(
                    newRound: Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                        Helpers.CreateConditionalSaved(
                            success: Helpers.Create<ContextActionRemoveBuff>(r => r.Buff = burningBuff),
                            failed: Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                                DiceType.Zero.CreateContextDiceValue(bonus: Helpers.CreateContextValueRank(AbilityRankType.DamageBonus)))))));

            feat.SetComponents(
                Helpers.Create<ApplyBuffOnFailedSaveToDamage>(a =>
                {
                    a.EnergyType = DamageEnergyType.Fire;
                    a.DurationValue = Helpers.CreateContextDuration(0, diceType: DiceType.D4, diceCount: 1);
                    a.Buff = burningBuff;
                }));
            return feat;
        }

        static BlueprintFeature CreateFirestorm()
        {
            // This is a cross between Fire Storm and Incendiary Cloud.
            // Like Fire Storm, it does 1d6 per caster level.
            // Like Incendiary Cloud, it's a persistent AOE (shorter duration though).

            var name = "MysteryFlameFirestorm";
            var cloudArea = library.CopyAndAdd<BlueprintAbilityAreaEffect>(
                            "a892a67daaa08514cb62ad8dcab7bd90", // IncendiaryCloudArea, used by brass golem breath
                            $"{name}Area", "3311da25c50643d0b7ba61da6d953cad");

            // TODO: offer the option to place more, using an activatable ability?
            cloudArea.Size = 15.Feet();
            cloudArea.SetComponents(
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.AsIs, AbilityRankType.DamageDice, classes: oracleArray),
                Helpers.CreateAreaEffectRunAction(round:
                    Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                        Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                            DiceType.D6.CreateContextDiceValue(AbilityRankType.DamageDice.CreateContextValue()),
                            halfIfSaved: true))));

            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "49ab0eaffc72414a872f8bf1b9372e0d", null);
            resource.SetFixedResource(1);

            var ability = Helpers.CreateAbility($"{name}Ability", "烈焰风暴",
                "以一个标准动作，你可以放出一股环绕着你的火焰。你可以制造每先知等级10尺见方的火焰。这些立方可以组合成你希望的任何形状，但每一块都必须与另一块相连，并且至少有一块与你相连。在这个火焰中的生物会受到1d6每等级的火焰伤害。通过反射检定可使伤害减半。火焰持续的时间等于你的魅力修正。你每天可以使用此能力1次。要选择此启示你至少要达到11级。",
                "4bddc1868e8b49ed8d27a67ee2085da3",
                Helpers.GetIcon("e3d0dfe1c8527934294f241e0ae96a8d"), // fire storm
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Close,
                "1 round/Charisma modifier", Helpers.reflexHalfDamage,
                resource.CreateResourceLogic(),
                FakeTargetsAround.Create(cloudArea.Size),
                Helpers.CreateSpellDescriptor(SpellDescriptor.Fire),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.StatBonus, ContextRankProgression.AsIs, stat: StatType.Charisma),
                Helpers.CreateRunActions(Helpers.Create<ContextActionSpawnAreaEffect>(c =>
                {
                    c.DurationValue = Helpers.CreateContextDuration();
                    c.AreaEffect = cloudArea;
                })));
            ability.CanTargetEnemies = true;
            ability.CanTargetFriends = true;
            ability.CanTargetPoint = true;
            ability.CanTargetSelf = true;
            ability.EffectOnAlly = AbilityEffectOnUnit.Harmful;
            ability.EffectOnEnemy = AbilityEffectOnUnit.Harmful;

            var feat = Helpers.CreateFeature(name, ability.Name, ability.Description,
                "b27101ce8b794188b246c4f2be1bd142",
                ability.Icon, FeatureGroup.None,
                Helpers.PrerequisiteClassLevel(oracle, 11),
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateCinderDance()
        {
            var feat = Helpers.CreateFeature("MysteryFlameCinderDance", "余烬之舞",
                "你的基础移动速度提高10英尺，在10级时，你在移动时可忽视困难地形带来的移动阻碍。有跛足诅咒的先知无法选择此启示。",
                "87cdb2075e0f4212895facaeb68cc3ab",
                Helpers.GetIcon("4f8181e7a7f1d904fbaea64220e83379"), // ExpeditiousRetreat
                FeatureGroup.None);

            var ignoreTerrain = Helpers.CreateFeature($"{feat.name}IgnoreTerrain", "余烬之舞", feat.Description,
                "7a83ca46db704d6e991559795f088906",
                Helpers.GetIcon("f3c0b267dd17a2a45a40805e31fe3cd1"), // FeatherStep
                FeatureGroup.None,
                UnitCondition.DifficultTerrain.CreateImmunity());

            feat.SetComponents(
                Helpers.PrerequisiteNoFeature(OracleCurses.lameCurse),
                Helpers.Create<BuffMovementSpeed>(b => b.Value = 10),
                ignoreTerrain.CreateAddFactOnLevelRange(oracle, 10));
            return feat;
        }

        static BlueprintFeature CreateHeatAura()
        {
            var feat = Helpers.CreateFeature("MysteryFlameHeatAura", "热能对流",
                "以一个迅捷动作，你能以你为中心制造一波热量辐射。这股热量对10尺之内的所有目标造成每两个先知等级1d4点火焰伤害（最少1d4点）。通过反射可使伤害减半。此外，你的身形在灼热的空气中显得扭曲而模糊，使你获得20%隐蔽，直到你下一轮。你每天可以使用此能力1次，在5级以及之后的每5级多一次使用次数。",
                "0eeea06535d349e7bafb3e8dcc538fa4",
                FireSpells.wallOfFire.Icon, FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "5b7cee2ea9d442dfb60308446cc83189", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var blurBuff = library.Get<BlueprintBuff>("dd3ad347240624d46a11a092b4dd4674");
            var buff = library.CopyAndAdd(blurBuff, $"{feat.name}Buff", "SetNameDescriptionIcon");
            buff.SetNameDescriptionIcon(feat);

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "10155b8f3e87419a819f09245f678cf6", feat.Icon, AbilityType.Supernatural, CommandType.Swift,
                AbilityRange.Personal, Helpers.oneRoundDuration, Helpers.reflexHalfDamage,
                Helpers.CreateRunActions(
                    Helpers.CreateConditional(Helpers.Create<ContextConditionIsEnemy>(),
                        Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                            Helpers.CreateActionDealDamage(
                                DamageEnergyType.Fire,
                                DiceType.D4.CreateContextDiceValue(),
                                isAoE: true, halfIfSaved: true))),
                    Helpers.CreateConditional(Helpers.Create<ContextConditionIsCaster>(),
                        buff.CreateApplyBuff(Helpers.CreateContextDuration(1, DurationRate.Rounds),
                            fromSpell: false, dispellable: false, toCaster: true))),
                Helpers.CreateAbilityTargetsAround(10.Feet(), TargetType.Any),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.CharacterLevel, ContextRankProgression.Div2,
                    min: 1, classes: oracleArray),
                resource.CreateResourceLogic());
            ability.EffectOnEnemy = AbilityEffectOnUnit.Harmful;
            ability.EffectOnAlly = AbilityEffectOnUnit.None;
            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateFireBreath()
        {
            var feat = Helpers.CreateFeature("MysteryFlameBreath", "烈焰喷发",
                "以一个标准动作，你可以从口中喷出一个15尺锥形火焰。这个火焰会造成1d4每等级的火焰伤害，最大10d4。通过反射检定可使伤害减半。你每天可以使用此能力1次，在5级以及之后的每5级再加1次使用次数。该豁免DC基于魅力。",
                "3f254ece8752466d8fd76ca358990eb9",
                Helpers.GetIcon("2a711cd134b91d34ab027b50d721778b"), // gold dragon fire breath
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "f3cc34b0ed9c4457b0b7ad4d1f2cbdd4", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var ability = library.CopyAndAdd<BlueprintAbility>("4783c3709a74a794dbe7c8e7e0b1b038", // burning hands
                $"{feat.name}Ability", "67c3d034c25c4daf8c55ec97f7af116b");
            ability.Type = AbilityType.Supernatural;
            ability.ReplaceContextRankConfig(c =>
            {
                Helpers.SetField(c, "m_UseMax", false);
                Helpers.SetField(c, "m_Max", 20);
            });
            var components = ability.ComponentsArray.WithoutSpellComponents().ToList();
            components.Add(resource.CreateResourceLogic());
            ability.SetComponents(components);

            feat.SetComponents(
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact(),
                OracleClass.CreateBindToOracle(ability));
            return feat;
        }

        static BlueprintFeature CreateFormOfFlame()
        {
            var feat = Helpers.CreateFeature("MysteryFlameForm", "烈焰形态",
                "以一个标准动作，你可以变成小型火元素，如同“元素形态I”。9级你可以变成中型火元素，如同“元素形态II”。11级你可以变成大型火元素，如同“元素形态III”。13级你可以变成超大型火元素，如同“元素形态IV”。你每天可以使用此能力1次，持续时间为每先知等级1小时。要选择此启示你至少要达到7级。",
                "1c18972911284249a0bc38f6f4ec4304",
                Helpers.GetIcon("bb6bb6d6d4b27514dae8ec694433dcd3"), // elemental body fire 1
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "e86f5231a48548f2a4d76a9e664acfab", null);
            resource.SetFixedResource(1);

            var formIds = new string[] {
                "bb6bb6d6d4b27514dae8ec694433dcd3",
                "9a87d2fb0e288664c8dff299ff030a38",
                "2c40b391368f05e4b91aa8a8a51109ef",
                "c281eeecc554b72449fef43924e522ce"
            };

            var components = new List<BlueprintComponent> {
                Helpers.PrerequisiteClassLevel(oracle, 7),
                resource.CreateAddAbilityResource()
            };
            var abilities = new List<BlueprintAbility>();
            for (int i = 0; i < formIds.Length; i++)
            {
                var spellId = formIds[i];
                var spell = library.Get<BlueprintAbility>(spellId);
                var ability = DragonMystery.CopyBuffSpellToAbility(spell, $"{feat.name}{i + 1}",
                    Helpers.MergeIds("7bcf2c58cd3f4b64862c7d3b33daa305", spellId),
                    AbilityType.Supernatural,
                    feat.Description,
                    resource, DurationRate.Hours);
                abilities.Add(ability);

                var isLast = i == formIds.Length - 1;
                var minLevel = 7 + i * 2;
                var maxLevel = isLast ? 20 : 8 + i * 2;
                components.Add(ability.CreateAddFactOnLevelRange(oracle, minLevel, maxLevel));
            }
            components.Add(OracleClass.CreateBindToOracle(abilities.ToArray()));
            feat.SetComponents(components);
            return feat;
        }

        static BlueprintFeature CreateMoltenSkin()
        {
            var feat = Helpers.CreateFeature("MysteryFlamesMoltenSkin", "熔岩皮肤",
                "你获得5点火焰抗力。5级该抗力变为10，11级变为20。17级你免疫火焰。",
                "9fc56e88f4dc4733bc99fad0be185ad5",
                Helpers.GetIcon("ddfb4ac970225f34dbff98a10a4a8844"),
                FeatureGroup.None);

            var resistFeat = Helpers.CreateFeature($"{feat.name}1", feat.Name, feat.Description,
                "92bc358da81044a9b763d650fcde6520",
                feat.Icon, FeatureGroup.None,
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel,
                    ContextRankProgression.Custom,
                    classes: oracleArray,
                    customProgression: new (int, int)[] {
                        (4, 5),
                        (10, 10),
                        (20, 20)
                    }),
                Helpers.Create<AddDamageResistanceEnergy>(a =>
                {
                    a.Type = DamageEnergyType.Fire;
                    a.Value = Helpers.CreateContextValueRank();
                }));

            var immunityFeat = Helpers.CreateFeature($"{feat.name}2", feat.Name, feat.Description,
                "4952794953fa49e284aae0df74727b43",
                feat.Icon, FeatureGroup.None,
                Helpers.Create<AddEnergyDamageImmunity>(a => a.EnergyType = DamageEnergyType.Fire));

            feat.SetComponents(
                resistFeat.CreateAddFactOnLevelRange(oracle, 1, 16),
                immunityFeat.CreateAddFactOnLevelRange(oracle, 17));
            return feat;
        }

        static BlueprintFeature CreateTouchOfFlame()
        {
            var feat = Helpers.CreateFeature("MysteryFlameTouch", "火焰之触",
                "以一个标准动作，你可以通过近战接触攻击对敌人造成“1d6+1/2先知等级”火焰伤害。你每天可以使用此能力的次数为“3+魅力修正”。\n" +
                "11级你持有的任何武器都将成为炽焰武器。",
                "b4b0a59bf8d645c0b4329e26176e1cc0",
                Helpers.GetIcon("05b7cbe45b1444a4f8bf4570fb2c0208"), // burning hands
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "8a8873e5c3664fdda9ce9543ca12289e", feat.Icon);
            resource.SetIncreasedByStat(3, StatType.Charisma);

            var spell = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "1464082280b54185b1ea7d5a108e1a72", feat.Icon,
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Touch, "", "",
                Helpers.CreateRunActions(
                    Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                        DiceType.D6.CreateContextDiceValue(1, bonus: Helpers.CreateContextValueRank()))),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2, classes: oracleArray),
                Helpers.CreateDeliverTouch());
            spell.CanTargetEnemies = true;
            spell.CanTargetFriends = true;
            spell.CanTargetSelf = true;
            spell.EffectOnEnemy = AbilityEffectOnUnit.Harmful;

            var flamingEnchant = library.Get<BlueprintWeaponEnchantment>("30f90becaaac51f41bf56641966c4121");
            var flamingWeapon = Helpers.CreateFeature($"{feat.name}Flaming", "${feat.Name} — 火焰武器", feat.Description,
                "8eb6532176974cdbbf20c38c0f433bad",
                Helpers.GetIcon("05b7cbe45b1444a4f8bf4570fb2c0208"), // arcane weapon flaming
                FeatureGroup.None,
                Helpers.Create<EnchantWeaponOnEquipLogic>(e => e.Enchantment = flamingEnchant));

            var touchSpell = spell.CreateTouchSpellCast(resource);
            feat.SetComponents(
                resource.CreateAddAbilityResource(),
                touchSpell.CreateAddFact(),
                flamingWeapon.CreateAddFactOnLevelRange(oracle, 11));
            return feat;
        }

        static BlueprintFeature CreateWingsOfFire()
        {
            var redDragonWings = library.Get<BlueprintActivatableAbility>("b00344e4b4134bb42a374ad8971392fd");
            var feat = Helpers.CreateFeature("MysteryFlameWings",
                "烈焰羽翼",
                "以一个迅捷动作，你可以显化一对火焰构成的翅膀，并给你提供60尺的飞行速度，机动性一般。你每天可以使用这对翅膀每先知等级1分钟，时间不必连续，但每次至少花费1分钟。要选择此启示你至少要达到7级。",
                "bec6ab62414447909cc1393982c10b62",
                redDragonWings.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "0b5a61426c76494da53c6ece0da081a2", feat.Icon);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            var ability = library.CopyAndAdd(redDragonWings, $"{feat.name}Ability", "744db4826b1a4d2fa43478e0a31304a2");

            // Activation requires a swift action.
            ability.ActivationType = AbilityActivationType.WithUnitCommand;
            Helpers.SetField(ability, "m_ActivateWithUnitCommand", CommandType.Swift);
            var inspireCourage = library.Get<BlueprintActivatableAbility>("2ce9653d7d2e9d948aa0b03e50ae52a6");
            ability.ActivateWithUnitAnimation = inspireCourage.ActivateWithUnitAnimation;

            ability.AddComponent(Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.TurnOn));
            ability.Buff = LingeringBuffLogic.CreateBuffForAbility(redDragonWings.Buff,
                "0f29a6f315984434b558661c1913f5e0", resource, DurationRate.Minutes);

            feat.SetComponents(
                Helpers.PrerequisiteClassLevel(oracle, 7),
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateFinalRevelation()
        {
            // Note: this was reworked a bit, because 3 of the 4 metamagics aren't in game:
            // - Enlarge, Silent, Still, and Extend.
            // (and they wouldn't be all that useful in a CRPG if implemented.)
            // 
            // So instead we'll offer:
            // - Reach, Intensified and Extend.
            //
            // Those have the same cost and seem somewhat on-theme for the Flame revelation.
            var feat = Helpers.CreateFeature("MysteryFlameFinalRevelation", "最终启示",
                "当达到20级时，你成为火焰的大师。你可以将以下任何一个专长应用于你的火系法术而不需要提高法术等级：法术增远，法术强效或法术延时。",
                "afdb91ba551440afad8b506b03f3aa5d",
                Helpers.GetIcon("98734a2665c18cd4db71878b0532024a"), // firebrand
                FeatureGroup.None);


            feat.SetComponents(Helpers.CreateAddFacts(ExclusiveAbilityToggle.AddToAbilities(
                CreateMetamagicAbility(feat, "Extend", "延时超魔 (火焰)", Metamagic.Extend, "bb491aec901f43ebabc1c2a651b1c690"),
                CreateMetamagicAbility(feat, "Intensified", "强效超魔 (火焰)", (Metamagic)ModMetamagic.Intensified, "3fe9e72e1e1a4fd3b2cb871b8068c258"),
                CreateMetamagicAbility(feat, "Reach", "增远超魔 (火焰)", Metamagic.Reach, "5b3c0178959641b08de7c9280ca19f3e"))));
            return feat;
        }

        static BlueprintActivatableAbility CreateMetamagicAbility(BlueprintFeature feat, String name, String displayName, Metamagic metamagic, String assetId)
        {
            var buff = Helpers.CreateBuff($"{feat.name}{name}Buff", displayName, feat.Description,
                assetId, feat.Icon, null,
                Helpers.Create<AutoMetamagic>(a => { a.Metamagic = metamagic; a.Descriptor = SpellDescriptor.Fire; }));

            var ability = Helpers.CreateActivatableAbility($"{feat.name}{name}ToggleAbility", displayName, feat.Description,
                Helpers.MergeIds(assetId, "86ac2af5f1724481b8ccf38addd9b552"), feat.Icon, buff, AbilityActivationType.Immediately,
                CommandType.Free, null);
            return ability;
        }
    }


    public class EnchantWeaponOnEquipLogic : OwnedGameLogicComponent<UnitDescriptor>, IUnitEquipmentHandler
    {
        public BlueprintItemEnchantment Enchantment;

        public override void OnFactActivate()
        {
            var body = Owner.Body;
            var weapons = new WeaponSlot[] {
                body.PrimaryHand,
                body.SecondaryHand,
            }.Concat(body.AdditionalLimbs).Select(w => w.MaybeWeapon).Where(w => w != null);
            weapons.ForEach(AddEnchant);
        }

        public void HandleEquipmentSlotUpdated(ItemSlot slot, ItemEntity previousItem)
        {
            if (slot.Owner != Owner) return;

            var weapon = (slot as WeaponSlot)?.MaybeWeapon;
            if (weapon != null) AddEnchant(weapon);
        }

        void AddEnchant(ItemEntityWeapon item)
        {
            var fact = item.Enchantments.GetFact(Enchantment);
            if (fact != null && !fact.IsTemporary) return;

            item.RemoveEnchantment(fact);
            var context = Helpers.GetMechanicsContext() ?? new MechanicsContext(Owner.Unit, Owner, Fact.Blueprint);
            var enchant = item.AddEnchantment(Enchantment, context);
            enchant.RemoveOnUnequipItem = true;
        }
    }

    class ApplyBuffOnFailedSaveToDamage : RuleInitiatorLogicComponent<RuleDealDamage>
    {
        public DamageEnergyType EnergyType;
        public BlueprintBuff Buff;
        public ContextDurationValue DurationValue;

        public override void OnEventAboutToTrigger(RuleDealDamage evt) { }
        public override void OnEventDidTrigger(RuleDealDamage evt)
        {
            try
            {
                var context = Helpers.GetMechanicsContext();
                var spellContext = context?.SourceAbilityContext;
                var target = Helpers.GetTargetWrapper()?.Unit;
                if (spellContext == null || target == null) return;

                if (spellContext.AbilityBlueprint.Type == AbilityType.Spell &&
                    evt.Damage > 0 && context.SavingThrow?.IsPassed == false &&
                    evt.DamageBundle.Any(b => (b as EnergyDamage)?.EnergyType == EnergyType))
                {
                    var rounds = DurationValue.Calculate(context);
                    Log.Write($"Burning Magic: applying fire damage to `{target.CharacterName}` for {rounds} rounds.");
                    target.Buffs.AddBuff(Buff, context, rounds.Seconds);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }


    class SpellLevelPropertyGetter : PropertyValueGetter
    {
        internal static readonly Lazy<BlueprintUnitProperty> Blueprint = new Lazy<BlueprintUnitProperty>(() =>
        {
            var p = Helpers.Create<BlueprintUnitProperty>();
            p.name = "SpellLevelCustomProperty";
            Main.library.AddAsset(p, "a01545ff992d404181e050a119a35a61");
            p.SetComponents(Helpers.Create<SpellLevelPropertyGetter>());
            return p;
        });

        public override int GetInt(UnitEntityData unit)
        {
            return Helpers.GetMechanicsContext()?.SourceAbilityContext?.SpellLevel ?? 0;
        }
    }

    public class ExclusiveAbilityToggle : BuffLogic
    {
        public BlueprintActivatableAbility[] Abilities;

        public static BlueprintActivatableAbility[] AddToAbilities(params BlueprintActivatableAbility[] abilities)
        {
            var e = Helpers.Create<ExclusiveAbilityToggle>();
            e.Abilities = abilities;
            foreach (var a in abilities) a.AddComponent(e);
            return abilities;
        }

        public override void OnTurnOn()
        {
            foreach (var ability in Owner.ActivatableAbilities.Enumerable)
            {
                if (ability.IsOn && Abilities.Contains(ability.Blueprint) && ability.Blueprint.Buff != Buff.Blueprint)
                {
                    Log.Write($"Turn off exclusive ability toggle: {ability.Name}");
                    ability.IsOn = false;
                }
            }
        }
    }
}
