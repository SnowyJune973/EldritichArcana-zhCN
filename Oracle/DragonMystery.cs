// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Localization;
using Kingmaker.RuleSystem;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using static Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class DragonMystery
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static (BlueprintFeature, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            var skill1 = StatType.SkillPerception;
            var skill2 = StatType.SkillKnowledgeArcana;
            var mystery = Helpers.CreateFeatureSelection("MysteryDragonSelection", "巨龙秘视域", $"{mysteryDescription}\n" +
                "追寻着巨龙的神秘并从那原始的魔力中汲取力量的先知们常常在这条道路上踽踽独行。他们往往深思熟虑，聪明骄傲。但与此同时，巨龙们的暴躁个性也渗透进了他们的灵魂中。\n" +
                "在选择这个启示时，先知们必须选择一种能量类型 (酸，冷，电或者火) 作为他的专注元素。这会影响几个他所获得的启示。\n" +
                $"本职技能: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}",
                "aec53bfbee334a0e93b90a283d4e308d",
                Helpers.GetIcon("da48f9d7f697ae44ca891bfc50727988"), // Blood of Dragons selection
                UpdateLevelUpDeterminatorText.Group);

            var classSkills = new BlueprintComponent[] {
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat)
            };

            BlueprintProgression acid, cold, electric, fire;
            mystery.SetFeatures(
                acid = CreateMysteryForEnergy(mystery, classSkills, DamageEnergyType.Acid, SpellDescriptor.Acid, "8e339ab3898fdd14b879753eaaae933d", "3d77ee3fc4913c44b9df7c5bbcdc4906"), // copper dragon acid breath, protection from acid
                cold = CreateMysteryForEnergy(mystery, classSkills, DamageEnergyType.Cold, SpellDescriptor.Cold, "cd36514cf1f38f84a977a265cec113ae", "021d39c8e0eec384ba69140f4875e166"), // silver dragon cold breath, protection from cold
                electric = CreateMysteryForEnergy(mystery, classSkills, DamageEnergyType.Electricity, SpellDescriptor.Electricity, "f97e345b9f474764fae2b7eff1c1a1c7", "e24ce0c3e8eaaaf498d3656b534093df"), // bronze dragon elecric breath, protection from electric
                fire = CreateMysteryForEnergy(mystery, classSkills, DamageEnergyType.Fire, SpellDescriptor.Fire, "2a711cd134b91d34ab027b50d721778b", "3f9605134d34e1243b096e1f6cb4c148")); // gold dragon fire breath, protection from fire

            var energyFeats = mystery.Features;
            var revelations = new List<BlueprintFeature> {
                CreateBreathWeapon(energyFeats),
                CreateDragonMagic(),
                CreateDragonSenses(),
                CreateFormOfTheDragon(),
                CreatePresenceOfDragons(),
                CreateScaledToughness(),
                CreateTailSwipe(),
                CreateWingsOfTheDragon(),
            };
            var description = new StringBuilder(mystery.Description).AppendLine();
            description.AppendLine("选择了巨龙秘视域的先知可以选择以下启示:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
            }
            revelations.AddRange(new BlueprintFeature[] {
                CreateDragonResistanceToEnergy(acid, DamageEnergyType.Acid, "fedc77de9b7aad54ebcc43b4daf8decd"), // resist acid
                CreateDragonResistanceToEnergy(cold, DamageEnergyType.Cold, "5368cecec375e1845ae07f48cdc09dd1"), // resist cold
                CreateDragonResistanceToEnergy(electric, DamageEnergyType.Electricity, "90987584f54ab7a459c56c2d2f22cee2"), // resist electric
                CreateDragonResistanceToEnergy(fire, DamageEnergyType.Fire, "ddfb4ac970225f34dbff98a10a4a8844"), // resist fire
                CreateTalonsOfTheDragon(acid, DamageEnergyType.Acid, "b522759a265897b4f8f7a1a180a692e4"), // acid (copper)
                CreateTalonsOfTheDragon(cold, DamageEnergyType.Cold, "c7d2f393e6574874bb3fc728a69cc73a"), // cold (silver)
                CreateTalonsOfTheDragon(electric, DamageEnergyType.Electricity, "7e0f57d8d00464441974e303b84238ac"), // electricity (bronze)
                CreateTalonsOfTheDragon(fire, DamageEnergyType.Fire, "6c67ef823db8d7d45bb0ef82f959743d"), // fire (gold)
            });
            description.AppendLine($"• Draconic Resistances");
            description.AppendLine($"• Talons of the Dragon");
            var descriptionStr = description.ToString();
            mystery.SetDescription(descriptionStr);
            foreach (var choice in mystery.Features)
            {
                choice.SetDescription(descriptionStr);
            }
            var revelation = Helpers.CreateFeatureSelection("MysteryDragonRevelation", "巨龙启示",
                mystery.Description, "b5bff56fe6cc4ca192df65f5ced050c9", null, FeatureGroup.None,
                mystery.PrerequisiteFeature());
            revelation.Mode = SelectionMode.OnlyNew;
            revelations.Add(UndoSelection.Feature.Value);
            revelation.SetFeatures(revelations);
            return (mystery, revelation);
        }

        static BlueprintFeatureSelection CreateDragonMagic()
        {
            // Note: this ability was reworked a bit from PnP.
            // It should add the spells as spell-like abilities, not as spells known.
            //
            // It's tricky to implement as SLA: we'd need BindAbilitiesToClass and resource logic,
            // which would require cloning all spells in the game into spell-like ability versions,
            // just in case they're selected. It's a pretty big cost for this one revelation.
            //
            // In terms of mechanics it's a bit of a wash:
            // - SLAs don't count against your spells per day, but can only be cast a fixed amount
            // - SLAs don't require material components, but they can't use metamagic
            //
            // (Wish/Limited Wish/Miracle do use SLAs like this, but they use a different resource pool.)
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var name = "MysteryDragonMagic";
            var feat = Helpers.CreateFeatureSelection($"{name}Selection", "龙之魔力",
                "你来自于巨龙的庞大魔力可以给予你模仿有限的奥术法术的能力。从法师/术士列表中选择一个比你当前可释放的最高环位的法术低两环的法术，或者两个比你当前可释放的最高环位的法术低三环的法术，将他们加入你的已知法术中，并且视作神术释放。",
                "e8c546fcff0b4734a787281cb5d37d32",
                Helpers.GetIcon("55edf82380a1c8540af6c6037d34f322"), // elven magic
                FeatureGroup.None,
                noFeature,
                PrerequisiteCasterSpellLevel.Create(oracle, 3));
            noFeature.Feature = feat;

            var pickOneSpell = Helpers.CreateParamSelection<SelectAnySpellAtComputedLevel>(
                $"{name}OneSpellSelection",
                "龙之魔力 (one spell)",
                feat.Description,
                "6215b0ab90574ed1b228b70d83959694",
                null,
                FeatureGroup.None,
                Helpers.wizardSpellList.CreateLearnSpell(oracle));
            pickOneSpell.SpellList = Helpers.wizardSpellList;
            pickOneSpell.SpellcasterClass = oracle;
            pickOneSpell.SpellLevelPenalty = 2;

            var pickTwoSpells = Helpers.CreateFeature($"{name}TwoSpellProgression",
                "龙之魔力 (two spells)",
                feat.Description,
                "b76fa49eab1e4fa7bdb2a6e678b7ba2b",
                null,
                FeatureGroup.None,
                PrerequisiteCasterSpellLevel.Create(oracle, 4));

            var pickSpellChoice1 = CreateDragonMagicSpellSelection(feat, 1, "6b36673c1912498c9977d1774f7ca834");
            var pickSpellChoice2 = CreateDragonMagicSpellSelection(feat, 2, "1cffc8df18884a74bf18b40c7284515a");
            SelectFeature_Apply_Patch.onApplyFeature.Add(pickTwoSpells, (state, unit) =>
            {
                var choice = pickSpellChoice1;
                state.AddSelection(null, choice, choice, 1);
                choice = pickSpellChoice2;
                state.AddSelection(null, choice, choice, 1);
            });

            feat.SetFeatures(pickOneSpell, pickTwoSpells);
            return feat;
        }

        static BlueprintFeatureSelection CreateDragonMagicSpellSelection(BlueprintFeature feat, int index, String assetId)
        {
            var pickSpell = Helpers.CreateFeatureSelection($"{feat.name}Spell{index}",
                    feat.Name, feat.Description, assetId, feat.Icon,
                    FeatureGroup.None);
            var wizardList = Helpers.wizardSpellList;
            Traits.FillSpellSelection(pickSpell, 1, 6, wizardList, (level) => new BlueprintComponent[] {
                PrerequisiteCasterSpellLevel.Create(oracle, level + 3),
                wizardList.CreateLearnSpell(oracle, level)
            }, oracle);
            return pickSpell;
        }

        static BlueprintFeature CreateDragonSenses()
        {
            // Note: this ability was reworked a bit, because the game does not have low-light vision/darkvision,
            // and it's tricky to code the "blindsense 30ft, or +30ft if you already had it".
            //
            // To capture the spirit of the PnP revelation, it now grants:
            //
            // +2 Perception
            // +4 Perception at level 5
            // Blindsense 30ft at level 11
            // Blindsense 60ft at level 15
            var feat = Helpers.CreateFeature("MysteryDragonSenses", "龙之感知",
                "你的感官变得更加敏锐。你在察觉检定中获得+2加值。在5级，此加值增加到+4。在11级，你获得30尺范围盲感，在15级，盲感范围增加到60尺。",
                "42ab3372fc2c473190d470a4ec398ae9",
                Helpers.GetIcon("82962a820ebc0e7408b8582fdc3f4c0c"), // sense vitals
                FeatureGroup.None);
            feat.SetComponents(
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel,
                    ContextRankProgression.Custom, classes: oracleArray,
                    customProgression: new (int, int)[] {
                        (4, 2),
                        (20, 4)
                    }),
                Helpers.CreateAddContextStatBonus(StatType.SkillPerception, ModifierDescriptor.UntypedStackable, ContextValueType.Rank),
                CreateBlindsenseFeat(30, feat, "1177237c49284b40ae48886a1439cd38").CreateAddFactOnLevelRange(oracle, 11, 14),
                CreateBlindsenseFeat(60, feat, "df59fa05a2744c889b808ed63f45eca4").CreateAddFactOnLevelRange(oracle, 15));
            return feat;
        }

        static BlueprintFeature CreateBlindsenseFeat(int range, BlueprintFeature parent, String assetId)
        {
            var draconDiscipleBlindsense = library.Get<BlueprintFeature>("bb19b581f5a47b44abfe00164f1fcade");
            return Helpers.CreateFeature($"{parent.name}Blindsense{range}",
                draconDiscipleBlindsense.Name, parent.Description, assetId,
                draconDiscipleBlindsense.Icon, FeatureGroup.None,
                Helpers.Create<Blindsense>(b => b.Range = range.Feet()));
        }

        static BlueprintFeature CreateFormOfTheDragon()
        {
            var formSpell1 = library.Get<BlueprintAbility>("f767399367df54645ac620ef7b2062bb");
            var formSpell2 = library.Get<BlueprintAbility>("666556ded3a32f34885e8c318c3a0ced");
            var formSpell3 = library.Get<BlueprintAbility>("1cdc4ad4c208246419b98a35539eafa6");

            var feat = Helpers.CreateFeature("MysteryDragonForm", "龙之形态",
                "你与龙神的紧密联系让你可以变成巨龙。 以一个标准动作，你可以变成中等龙的形态如同变化巨龙I。 在15级，你可以变成大型龙的形态如同变化巨龙II。 在19级，你可以变成巨龙的形态如同变化巨龙III。 你每天可以使用1此此能力。 但是持续时间为每先知等级10分钟。如果你至少有15级先知等级，并且决定使用此能力如同变化巨龙I, 则此能力持续时间变为每先知等级一小时。",
                "48accc5693ff4704a16938d365ac7d36",
                formSpell1.Icon,
                FeatureGroup.None);
            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "599dd4ff4ed346769928da46da35dbb4", null);
            resource.SetFixedResource(1);

            var formAbility1 = CreateFormOfTheDragonAbility(feat, formSpell1, resource, DurationRate.TenMinutes);
            var formAbility1Long = CreateFormOfTheDragonAbility(feat, formSpell1, resource, DurationRate.Hours);
            var formAbility2 = CreateFormOfTheDragonAbility(feat, formSpell2, resource, DurationRate.TenMinutes);
            var formAbility3 = CreateFormOfTheDragonAbility(feat, formSpell3, resource, DurationRate.TenMinutes);

            feat.SetComponents(
                Helpers.PrerequisiteClassLevel(oracle, 11),
                resource.CreateAddAbilityResource(),
                formAbility1.CreateAddFactOnLevelRange(oracle, 11, 14),
                formAbility1Long.CreateAddFactOnLevelRange(oracle, 15),
                formAbility2.CreateAddFactOnLevelRange(oracle, 15, 18),
                formAbility3.CreateAddFactOnLevelRange(oracle, 19),
                OracleClass.CreateBindToOracle(formAbility1, formAbility1Long, formAbility2, formAbility3));
            return feat;
        }

        static BlueprintAbility CreateFormOfTheDragonAbility(BlueprintFeature parent, BlueprintAbility formSpell, BlueprintAbilityResource resource, DurationRate duration)
        {
            var durationText = duration == DurationRate.Hours ? Helpers.hourPerLevelDuration : Helpers.tenMinPerLevelDuration;
            var durationSuffix = duration == DurationRate.Hours ? "Hours" : "";
            var baseId = duration == DurationRate.Hours ? "0816ac2129104cc1bc32e06d0a3040b0" : "0bf924a419dd4ad78b64d917a910f002";

            var formAbility = library.CopyAndAdd(formSpell, $"MysterDragon{formSpell.name}{durationSuffix}",
                Helpers.MergeIds(baseId, formSpell.AssetGuid));
            formAbility.SetDescription(parent.Description);
            formAbility.LocalizedDuration = durationText;

            formAbility.Type = AbilityType.Supernatural;
            formAbility.SetComponents(
                Helpers.CreateSpellComponent(formSpell.School),
                Helpers.CreateSpellDescriptor(formSpell.SpellDescriptor),
                Helpers.CreateResourceLogic(resource),
                formAbility.CreateAbilityVariants(formSpell.Variants.Select(spell =>
                {
                    return CopyBuffSpellToAbility(spell, $"MysteryDragon{spell.name}{durationSuffix}",
                        Helpers.MergeIds(formAbility.AssetGuid, spell.AssetGuid),
                        AbilityType.Supernatural,
                        parent.Description,
                        resource, duration);
                })));
            return formAbility;
        }

        internal static BlueprintAbility CopyBuffSpellToAbility(BlueprintAbility spell, String name, String assetId, AbilityType abilityType, String description, BlueprintAbilityResource resource, DurationRate? newDuration = null)
        {
            var ability = library.CopyAndAdd(spell, name, assetId);
            ability.SetDescription(description);
            if (newDuration.HasValue) ability.LocalizedDuration = newDuration.Value.GetText();
            ability.Type = abilityType;
            List<BlueprintComponent> components;
            if (newDuration.HasValue)
            {
                components = ability.ComponentsArray.WithoutSpellComponents().Select(c =>
                {
                    var runAction = c as AbilityEffectRunAction;
                    if (runAction == null) return c;

                    var newAction = Helpers.CreateRunActions(runAction.Actions.Actions.Select(a =>
                    {
                        var applyBuff = a as ContextActionApplyBuff;
                        if (applyBuff == null) return a;
                        return Helpers.CreateApplyBuff(applyBuff.Buff,
                            Helpers.CreateContextDuration(rate: newDuration.Value),
                            fromSpell: false, dispellable: false);
                    }).ToArray());
                    newAction.SavingThrowType = runAction.SavingThrowType;
                    return newAction;
                }).ToList();
            }
            else
            {
                components = ability.ComponentsArray.ToList();
            }

            components.Add(Helpers.CreateResourceLogic(resource));
            ability.SetComponents(components);
            return ability;
        }

        static BlueprintFeature CreatePresenceOfDragons()
        {
            var feat = Helpers.CreateFeature("MysteryDragonPresence", "龙之威压",
                "那些敢于反抗你的人必须克服他们对龙的恐惧，或者在你如同巨龙般的威严下颤抖。以一个迅捷行动，你可以在你周围散布恐惧。当你激活此能力时，30尺内能看到你的敌人必须尝试意志豁免，成功则此生物在接下来24小时内对此能力免疫。当豁免失败时，敌人陷入战栗 2d6 轮。这是一个影响心灵的恐惧效果。你可以在1级时每日使用一次此能力。并且在5级和5级之后的每5级获得每日额外一次使用次数。",
                "94e000ab4ab3471087244cac565be105",
                Helpers.GetIcon("41cf93453b027b94886901dbfc680cb9"), // overwhelming presence
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource",
                "", "", "ad504b96505e4c2eb76c6d316ed5d8c0", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var presenceImmunity = Helpers.CreateBuff($"{feat.name}ImmuneForDay",
                $"免疫 {feat.Name}", "一次成功的豁免检定使你在24小时内对龙之威压能力免疫。",
                "a2c921fde72c40b3baccca9a54beafbd",
                Helpers.GetIcon("55a037e514c0ee14a8e3ed14b47061de"), // remove fear
                null);

            var shaken = library.Get<BlueprintBuff>("25ec6cb6ab1845c48a95f9c20b034220");
            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name,
                feat.Description, "ff6b4f892a8c4f489d13a42f748c7a11",
                feat.Icon, AbilityType.Supernatural, CommandType.Swift,
                AbilityRange.Medium, "2d6 rounds", "Will negates",
                Helpers.CreateAbilityTargetsAround(30.Feet(), TargetType.Enemy),
                Helpers.CreateSpellDescriptor(SpellDescriptor.Fear | SpellDescriptor.MindAffecting),
                Helpers.CreateResourceLogic(resource),
                Helpers.CreateRunActions(SavingThrowType.Will, Helpers.CreateConditionalSaved(
                    success: Helpers.CreateApplyBuff(presenceImmunity, Helpers.CreateContextDuration(1, DurationRate.Days), fromSpell: false, dispellable: false),
                    failed: Helpers.CreateApplyBuff(shaken, Helpers.CreateContextDuration(0, diceType: DiceType.D6, diceCount: 2),
                        fromSpell: false, dispellable: false))));

            presenceImmunity.AddComponent(Helpers.Create<AddSpellImmunity>(a =>
            {
                a.Type = SpellImmunityType.Specific;
                a.Exceptions = new BlueprintAbility[] { ability };
            }));

            ability.EffectOnEnemy = AbilityEffectOnUnit.Harmful;
            ability.CanTargetEnemies = true;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateScaledToughness()
        {
            var barkskin = library.Get<BlueprintBuff>("533592a86adecda4e9fd5ed37a028432");
            var feat = Helpers.CreateFeature("MysteryDragonScaledToughness", "龙之皮肤",
                "你可以将你的皮肤转变为龙鳞。每日一次，以迅捷动作，你可以硬化你的皮肤，让它呈现出鳞片状并且获得DR10/魔法。并且在此期间，你免疫麻痹和睡眠效果。此能力持续时间为每先知等级1轮。在13级时，你每日可以使用两次此能力。",
                "b61e6876f14b440e804878bf1fb85b72",
                barkskin.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "",
                 "7ee8232c8f9941e6b7e9acd51999309a", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 13, 1, 13, 0, 0, 0, oracleArray);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "8ee9c303c8e2447e990dc8d4a7d2ffc6", feat.Icon,
                barkskin.FxOnStart, Helpers.Create<AddDamageResistancePhysical>(a =>
                {
                    a.BypassedByMagic = true;
                    a.Value = 10;
                }),
                UnitCondition.Sleeping.CreateImmunity(),
                UnitCondition.Paralyzed.CreateImmunity(),
                (SpellDescriptor.Sleep | SpellDescriptor.Paralysis).CreateBuffImmunity());

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "d7d3e308fffc412eb2e5bba5ec4bc5d6", feat.Icon, AbilityType.Supernatural, CommandType.Swift,
                AbilityRange.Personal, Helpers.roundsPerLevelDuration, "",
                Helpers.CreateResourceLogic(resource),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.AsIs, classes: oracleArray),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(), fromSpell: false, dispellable: false)));
            ability.CanTargetSelf = true;

            feat.SetComponents(
                Helpers.PrerequisiteClassLevel(oracle, 7),
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateTailSwipe()
        {
            // TODO: implement this closer to rules as written, and update the description.
            //
            // For now, this is just an extra attack of opportunity.
            // While this may hit harder, RAW offers a free trip attempt, which is very nice.
            // 
            // Can probably implement the extra tail weapon attack using a combo of:
            // - a stat bonus to attackOfOpportunityCount (already implemented),
            // - listen for various rules/events, possibly:
            //   - IAttackOfOpportunityHandler.HandleAttackOfOpportunity,
            //   - RuleAttackWithWeapon.IsAttackOfOpportunity (created by UnitAttackOfOpportunity.OnAction)
            //   - UnitCombatState.AttackOfOpportunityCount == 1 (to know it's the tail swipe one)
            // - create an item for the tail
            return Helpers.CreateFeature("MysteryDragonTailSwipe", "龙之扫尾",
                "你可以变化出一条巨龙之尾来宣泄你的愤怒。你长出巨龙之尾，提升你的平衡性，让你每回合可以使用你的武器进行一次额外的借机攻击。",
                "895b438d49f748c684db8db865735cea",
                Helpers.GetIcon("02be9687e105dd742aeafbafff0c450e"), // weapon spec tail, greater
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.AttackOfOpportunityCount, 1, ModifierDescriptor.UntypedStackable));
        }

        static BlueprintFeature CreateTalonsOfTheDragon(BlueprintFeature mystery, DamageEnergyType energy, String bloodlineId)
        {
            // Note: this is identical to dragon bloodline claws, so we just need to link it up.
            var revelation = Helpers.CreateFeature($"MysteryDragonTalons{energy}", "龙之利爪",
                "你变化出巨龙的利爪协助你战斗。你可以以自由动作长出一只利爪。此利爪被视作天生武器，当你在进行全回合攻击时可以以你的满额攻击加值进行两次爪击。每一次攻击都会造成1d4点伤害（小型生物则为1d3）+你的力量调整值。在5级时，这些利爪被视作魔法武器以穿透伤害减免。在7级时，伤害骰增大一级成为1d6（小型为1d4）+你的力量调整值。在11级时，这些利爪会造成额外1d6点你选择的能量类型的伤害。你每日可以使用此能力的次数为3+你的魅力调整值。这些回合不必是连续的。",
                Helpers.MergeIds(mystery.AssetGuid, "805440d7056742dca6708b40f323d36e"),
                Helpers.GetIcon("3a27e888cae24684695182fc53554261"),
                FeatureGroup.None);

            var components = new List<BlueprintComponent> { mystery.PrerequisiteFeature() };
            var bloodline = library.Get<BlueprintProgression>(bloodlineId);

            var ability = EldritchHeritage.GetBloodlinePower(bloodline, 1);
            var entries = EldritchHeritage.CollectLevelEntries(1, ability, bloodline);

            foreach (var entry in entries)
            {
                var level = entry.Item1;

                // Fix up the AddFeatureOnClassLevel to include Oracle.
                var claws = entry.Item2;
                claws = library.CopyAndAdd(claws, $"{claws.name.Replace("BloodlineDraconic", "MysteryDragon")}", claws.AssetGuid, "4c01a82e897f4ddf8e52477c9bfd42ee");
                claws.SetComponents(claws.ComponentsArray.Select(c =>
                {
                    var addFeat = c as AddFeatureOnClassLevel;
                    if (addFeat == null) return c;
                    addFeat = UnityEngine.Object.Instantiate(addFeat);
                    addFeat.Class = oracle;
                    addFeat.AdditionalClasses = Array.Empty<BlueprintCharacterClass>();
                    addFeat.Archetypes = Array.Empty<BlueprintArchetype>();
                    return addFeat;
                }).ToArray());

                components.Add(claws.CreateAddFactOnLevelRange(oracle, level));
            }
            revelation.SetComponents(components);

            return revelation;
        }

        static BlueprintFeature CreateWingsOfTheDragon()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var revelation = Helpers.CreateFeatureSelection("MysteryDragonWingsSelection", "龙之翼",
                "如同巨龙一般，你可以从天而降的威吓你的敌人。以一个迅捷动作，你可以长出龙翼，使你的飞行速度达到60尺（机动性笨拙）。每天可使用每先知等级1分钟。这些时间不必是连续的，但必须以1分钟为单位使用。在11级时，你每天可以使用此能力每先知等级10分钟。在15级时，你可以无限的使用龙翼。",
                "ed6599167d3841ccafeeac5297546963",
                Helpers.GetIcon("e0bdad79800cbfd40afdbe7fdec2441e"), // Black dragon wings
                FeatureGroup.None,
                Helpers.PrerequisiteClassLevel(oracle, 7),
                noFeature);
            noFeature.Feature = revelation;

            var wingChoices = new List<BlueprintFeature>();
            // Give the player the option of any of the dragon wing colors
            var wingAbilityIds = new String[] {
                "a800d71694dc7634b9481c1cbf5b355f", // AbilityWingsDraconicBlack
                "7c46d0168cbc6884694f8392802a8f39", // AbilityWingsDraconicBlue
                "a5daeacf181c8bd44aea74b53542104c", // ... etc
                "dade4a4a146073945bb2b50c0d669d52",
                "74b13a9f82635454facf7e2456fda78f",
                "0d610d5c3713d5a46bca0833fad1847e",
                "76ade4bf20c5f48459734087cf3f9a86",
                "b00344e4b4134bb42a374ad8971392fd",
                "7679910a16368cc43b496cef2babe1cb",
                "04b894e77ecc2b14d9c4297c1faba0cb",
            };
            var resource = Helpers.CreateAbilityResource($"MysteryDragonWingsResource", "", "",
                 "a28dc64eca66412584d013c6bd587a81", revelation.Icon);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            foreach (var wingAbilityId in wingAbilityIds)
            {
                wingChoices.Add(CreateDragonWingFeature(revelation, resource, wingAbilityId));
            }
            revelation.SetFeatures(wingChoices);
            return revelation;
        }

        static BlueprintFeature CreateDragonWingFeature(BlueprintFeatureSelection selection, BlueprintAbilityResource resource, String wingAbilityId)
        {
            var wingsAbility = library.Get<BlueprintActivatableAbility>(wingAbilityId);
            var wingName = wingsAbility.name.Replace("AbilityWingsDraconic", "");
            var buff = wingsAbility.Buff;
            var feat = Helpers.CreateFeature(selection.name.Replace("Selection", wingName),
                $"{selection.Name} — {wingName}",
                selection.Description,
                Helpers.MergeIds("8329779cc41043d5baa350959d503030", wingAbilityId),
                wingsAbility.Icon,
                FeatureGroup.None);

            var wings1 = library.CopyAndAdd(wingsAbility, $"{feat.name}Ability1",
                Helpers.MergeIds("bd15fc3f81c74e64b4beb647149b2400", wingAbilityId));
            wings1.Buff = LingeringBuffLogic.CreateBuffForAbility(buff,
                "ca7c5f7e585749019f1077132bb21195", resource, DurationRate.Minutes, dispellable: false);
            wings1.AddComponent(Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));
            var wings2 = library.CopyAndAdd(wingsAbility, $"{feat.name}Ability2",
                Helpers.MergeIds("9c9c8c8cfb944e91a34c7a32187724a4", wingAbilityId));
            wings2.Buff = LingeringBuffLogic.CreateBuffForAbility(buff,
                "f9de5c6ca19b45fc955de8b4dd27bf05", resource, DurationRate.TenMinutes, dispellable: false);
            wings2.AddComponent(Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));

            feat.SetComponents(
                resource.CreateAddAbilityResource(),
                wings1.CreateAddFactOnLevelRange(oracle, 7, 10),
                wings2.CreateAddFactOnLevelRange(oracle, 11, 14),
                wingsAbility.CreateAddFactOnLevelRange(oracle, 15));
            return feat;
        }

        static BlueprintFeature CreateBreathWeapon(BlueprintFeature[] energyFeats)
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var resource = library.CopyAndAdd<BlueprintAbilityResource>("bebe2a97cc091934189fd8255e903b1f", // draconic breath weapon
                "MysteryDragonBreathWeaponResource", "ee32406e5fda47caa78ade554efad4d5");
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var feat = Helpers.CreateFeatureSelection("MysteryDragonBreathWeaponSelection",
                "龙之吐息", "巨龙的原始力量在你的体内流淌。你获得喷吐攻击。造成每两个先知等级1d6点你选择的能量类型的伤害（最小1d6点，反射豁免减半）。喷吐攻击的形状为30尺锥形或者60尺线状。在选择此启示时做出选择并且无法更改。在1级时，你每天可以使用此能力1此，在5级和5级之后的每5级获得每日一次的额外使用次数。",
                "881dd19e19eb498db0ce341368f29d86",
                Helpers.GetIcon("5e826bcdfde7f82468776b55315b2403"), // Dragon's Breath
                FeatureGroup.None,
                noFeature);
            noFeature.Feature = feat;

            // Note, two permutations aren't implemented:
            // - cold line
            // - electric cone
            // The game doesn't have an electric cone effect, so that'd be difficult to implement.
            var acid = energyFeats[0];
            var cold = energyFeats[1];
            var electric = energyFeats[2];
            var fire = energyFeats[3];
            feat.SetFeatures(
                CreateBreathWeaponFeature(/*copper*/ "63718b3248898134eba94a139ea07313", acid, "Acid", "Line", resource),
                CreateBreathWeaponFeature(/*green*/  "4d107a429575cb344bcba32a5b1a6abe", acid, "Acid", "Cone", resource),
                CreateBreathWeaponFeature(/*silver*/ "a1cde01a9790834449d8f547ca52fc88", cold, "Cold", "Cone", resource),
                CreateBreathWeaponFeature(/*bronze*/ "e86286b52aeefb540a67c3c1af235167", electric, "Electricity", "Line", resource),
                CreateBreathWeaponFeature(/*brass*/  "9d7bb2a6d590ba0498992f6ce825f2cc", fire, "Fire", "Line", resource),
                CreateBreathWeaponFeature(/*gold*/   "fcf0cb61b79b6fd47a6ed91f40820cea", fire, "Fire", "Cone", resource));
            return feat;
        }

        static BlueprintFeature CreateBreathWeaponFeature(String existingId, BlueprintFeature energyFeat, String energyName, String shapeName, BlueprintAbilityResource resource)
        {
            var name = $"MysteryDragon{energyName}{shapeName}Breath";
            var assetId = shapeName == "Line" ? "9cc10f40f706427daff880074c1c423c" : "99bee4ae51af40839154c17c434bca17";
            var feat = library.CopyAndAdd<BlueprintFeature>(existingId,
                $"{name}Feature", assetId, energyFeat.AssetGuid);
            feat.SetName($"喷吐攻击 — {shapeName} of {energyName}");

            var ability = library.CopyAndAdd(feat.GetComponent<AddFacts>().Facts[0] as BlueprintAbility,
                $"{name}Ability", feat.AssetGuid, "56e4f7ee4085483ab996b65b24671d8b");

            ability.ReplaceContextRankConfig(Helpers.CreateContextRankConfig(
                ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2,
                min: 1, classes: oracleArray));

            ability.ReplaceResourceLogic(resource);

            var cooldown = library.CopyAndAdd<BlueprintBuff>("b78d21189e7f6e943920236f009d30e3",
                $"{name}CooldownBuff", feat.AssetGuid, "8cfdf6e1dff2403097b7bea869e3294b");

            // Clone the breath weapon and remove resources; instead we'll use a cooldown.
            var finalAbility = library.CopyAndAdd(ability, $"{name}FinalAbility", feat.AssetGuid, "b8f86b7c48644308a065dc2a0c8fbd33");
            finalAbility.SetComponents(ability.ComponentsArray.Select(c =>
            {
                if (c is AbilityResourceLogic)
                {
                    var a = Helpers.Create<AbilityCasterHasNoFacts>();
                    a.Facts = new BlueprintUnitFact[] { cooldown };
                    return a;
                }
                var runAction = c as AbilityEffectRunAction;
                if (runAction != null)
                {
                    runAction = UnityEngine.Object.Instantiate(runAction);
                    runAction.Actions = Helpers.CreateActionList(runAction.Actions.Actions.AddToArray(
                        cooldown.CreateApplyBuff(Helpers.CreateContextDuration(1, diceType: DiceType.D4, diceCount: 1),
                            fromSpell: false, dispellable: false, toCaster: true)));
                    return runAction;
                }
                return c;
            }));

            feat.SetComponents(
                energyFeat.PrerequisiteFeature(),
                ability.CreateAddFactOnLevelRange(oracle, 1, 19),
                finalAbility.CreateAddFactOnLevelRange(oracle, 20),
                resource.CreateAddAbilityResource(),
                OracleClass.CreateBindToOracle(ability));
            return feat;
        }

        static BlueprintProgression CreateMysteryForEnergy(BlueprintFeatureSelection selection, BlueprintComponent[] classSkills, DamageEnergyType energy, SpellDescriptor descriptor, String iconId, String protectIconId)
        {
            var mystery = Helpers.CreateProgression($"MysteryDragon{energy}Progression",
                $"{selection.Name} — {energy}",
                selection.Description,
                Helpers.MergeIds("494bd9b106844c70a06dbf2648bf2a13", iconId),
                Helpers.GetIcon(iconId),
                selection.Groups[0],
                classSkills);
            mystery.Classes = oracleArray;

            var spells = Bloodlines.CreateSpellProgression(mystery, new String[] {
                "bd81a3931aa285a4f9844585b5d97e51", // cause fear
                "21ffef7791ce73f468b6fca4d9371e8b", // resist energy
                FlySpells.flySpell.AssetGuid,
                "d2aeac47450c76347aebbc02e4f463e0", // fear
                "0a5ddfbcfb3989543ac7c936fc256889", // spell resistance
                "f0f761b808dc4b149b08eaf44b99f633", // dispel magic greater (should be: antimagic field)
                "4cf3d0fae3239ec478f51e86f49161cb", // true seeing
                "1cdc4ad4c208246419b98a35539eafa6", // form of the dragon iii
                "41cf93453b027b94886901dbfc680cb9", // overwhelming presence
            });
            var entries = new List<LevelEntry>();
            for (int level = 1; level <= 9; level++)
            {
                entries.Add(Helpers.LevelEntry(level * 2, spells[level - 1]));
            }
            var finalRevelation = CreateFinalRevelation(mystery, energy, descriptor, protectIconId);
            entries.Add(Helpers.LevelEntry(20, finalRevelation));

            mystery.LevelEntries = entries.ToArray();
            mystery.UIGroups = Helpers.CreateUIGroups(new List<BlueprintFeatureBase>(spells) { finalRevelation });
            return mystery;
        }

        static BlueprintFeature CreateFinalRevelation(BlueprintProgression mystery, DamageEnergyType energy, SpellDescriptor descriptor, String resistIconId)
        {
            // Note: the 1d4+1 breath weapon cooldown is implemented in the breath weapon feat.
            var dragonType = library.Get<BlueprintFeature>("455ac88e22f55804ab87c2467deff1d6");
            return Helpers.CreateFeature($"MysteryDragonFinalRevelation{energy}",
                $"最终启示 — {energy}",
                "达到20级时，你的命运将一览无遗。你获得对麻痹，睡眠和你所选的能量类型的免疫。对于法术和魔法效果而言，你的物种变为龙。如果你拥有龙之吐息的启示，你可以无限制的使用你的喷吐攻击。但每1d4+1轮不超过1次。",
                Helpers.MergeIds("325e4b89871c41549c235cbe7dd1d948", mystery.AssetGuid),
                Helpers.GetIcon(resistIconId),
                FeatureGroup.None,
                dragonType.CreateAddFact(), // note: grants immunity to paralysis/sleep conditions
                Helpers.Create<AddEnergyDamageImmunity>(a => a.EnergyType = energy),
                (SpellDescriptor.Sleep | SpellDescriptor.Paralysis | descriptor).CreateBuffImmunity());
        }

        static BlueprintFeature CreateDragonResistanceToEnergy(BlueprintFeature mystery, DamageEnergyType energy, String iconId)
        {
            var result = Helpers.CreateFeature($"MysteryDragonResistances{energy}",
                $"龙之抗性 — {energy}",
                "如同巨龙一般，你不容易受到你所选的能量类型的伤害。你获得+1AC的天生护甲值和对于所选能量的5点抗性。在9级时，你获得的天生护甲值增加到+2，能量抗性增加到10。在15级时，你的天生护甲值增加到+4，能量抗性增加到20。",
                Helpers.MergeIds(mystery.AssetGuid, "f11c13c0684348ff8a2c9104391c5e77"),
                Helpers.GetIcon(iconId),
                FeatureGroup.None,
                mystery.PrerequisiteFeature(),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel,
                    ContextRankProgression.Custom, classes: oracleArray,
                    customProgression: new (int, int)[] {
                        (8, 1),
                        (14, 2),
                        (20, 4)
                    }),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel,
                    ContextRankProgression.Custom, AbilityRankType.StatBonus,
                    classes: oracleArray,
                    customProgression: new (int, int)[] {
                        (8, 5),
                        (14, 10),
                        (20, 20)
                    }),
                Helpers.CreateAddContextStatBonus(StatType.AC, ModifierDescriptor.NaturalArmor, ContextValueType.Rank),
                Helpers.Create<AddDamageResistanceEnergy>(a =>
                {
                    a.Type = energy;
                    a.Value = Helpers.CreateContextValueRank(AbilityRankType.StatBonus);
                })
            );

            return result;
        }
    }

    // Designed for Oracle Dragon Magic selection.
    // Selects a spell at a particular level, a certain number of levels below max level.
    //
    // Uses:
    // - SpellLevelPenalty (-2 for Dragon Magic)
    // - SpellcasterClass (Oracle)
    // - SpellList (Wizard/Sorcerer)
    public class SelectAnySpellAtComputedLevel : CustomParamSelection
    {
        protected override IEnumerable<BlueprintScriptableObject> GetItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            var spellLevel = previewUnit.GetSpellbook(SpellcasterClass).MaxSpellLevel;
            return SpellList.SpellsByLevel[spellLevel - SpellLevelPenalty].Spells;
        }

        protected override IEnumerable<BlueprintScriptableObject> GetAllItems() => Helpers.allSpells;

        protected override bool CanSelect(UnitDescriptor unit, FeatureParam param)
        {
            return !unit.GetSpellbook(SpellcasterClass).IsKnown(param.Blueprint as BlueprintAbility);
        }
    }
}
