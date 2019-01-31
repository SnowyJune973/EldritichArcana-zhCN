// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Text;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components.TargetCheckers;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Actions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using static Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class LifeMystery
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static BlueprintFeature enhancedCures, lifeLink;
        internal static BlueprintAbilityResource channelResource;

        internal static (BlueprintFeature, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            var revelations = new List<BlueprintFeature>()
            {
                CreateChannel(),
                CreateCombatHealer(),
                CreateDelayAffliction(),
                CreateEnergyBody(),
                CreateEnhancedCures(),
                CreateHealingHands(),
                CreateLifeLink(),
                CreateLifeSense(),
                CreateSafeCuring(),
                CreateSpiritBoost()
            };

            var skill1 = StatType.SkillAthletics;
            var skill2 = StatType.SkillLoreNature;
            var description = new StringBuilder(mysteryDescription).AppendLine();
            description.AppendLine(
                $"本职技能: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}\n" +
                "选择了生命秘视域的先知可以选择以下启示:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
            }

            var mystery = Helpers.CreateProgression("MysteryLifeProgression", "生命秘视域", description.ToString(),
                "a2c3c801deb84bc9bab6bd35e5290d5d",
                Helpers.GetIcon("a79013ff4bcd4864cb669622a29ddafb"), // channel energy
                UpdateLevelUpDeterminatorText.Group,
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat));
            mystery.Classes = oracleArray;

            var spells = Bloodlines.CreateSpellProgression(mystery, new String[] {
                "f6f95242abdfac346befd6f4f6222140", // remove sickness (should be: detect undead)
                "e84fc922ccf952943b5240293669b171", // lesser restoration
                "e7240516af4241b42b2cd819929ea9da", // neutralize poison
                "f2115ac1148256b4ba20788f7e966830", // restoration
                "d5847cad0b0e54c4d82d6c59a3cda6b0", // breath of life
                "5da172c4c89f9eb4cbb614f3a67357d3", // heal
                "fafd77c6bfa85c04ba31fdc1c962c914", // greater restoration
                "867524328b54f25488d371214eea0d90", // mass heal
                WishSpells.miracle.AssetGuid, // miracle (should be: true resurrection)
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

            var revelation = Helpers.CreateFeatureSelection("MysteryLifeRevelation", "生命启示",
                mystery.Description, "6949da6445394dabbfb327c000706122", null, FeatureGroup.None,
                mystery.PrerequisiteFeature());
            revelation.Mode = SelectionMode.OnlyNew;
            revelation.SetFeatures(revelations);
            return (mystery, revelation);
        }

        static BlueprintFeature CreateChannel()
        {
            var channelEnergy = library.Get<BlueprintAbility>("f5fc9a1a2a3c1a946a31b320d1dd31b2");
            var channelPositiveHarm = library.Get<BlueprintAbility>("279447a6bf2d3544d93a0a39c3b8e91d");

            var feat = Helpers.CreateFeature("MysteryLifeChannel", "导引能量",
                "你可以像牧师一样引导正能量。当决定你的导能的治疗效果或者对不死生物的伤害和豁免DC时，使用你的先知等级作为有效牧师等级。 " +
                "你每天可以导引能量的次数为1+你的魅力调整值。",
                "86763655ebbc406f8a4d9a58415847d5",
                channelEnergy.Icon, FeatureGroup.None);
            var resource = library.CopyAndAdd<BlueprintAbilityResource>("5e2bba3e07c37be42909a12945c27de7", // channel energy resource
                $"{feat.name}Resource", "fd1b7cfdcc2e4eb29c6ecdd4f5e378a4");
            resource.SetIncreasedByStat(1, StatType.Charisma);
            channelResource = resource;

            var lifeChannel = library.CopyAndAdd(channelEnergy, $"{feat.name}Heal", "bfa9138753e3431a99043773f94f40ee");
            var lifeChannelHarm = library.CopyAndAdd(channelPositiveHarm, $"{feat.name}HarmUndead", "ed2cda862d97431ea13dcbbf27359e3b");

            lifeChannel.ReplaceResourceLogic(resource);
            lifeChannel.ReplaceContextRankConfig(Helpers.CreateContextRankConfig(
                ContextRankBaseValueType.ClassLevel, ContextRankProgression.OnePlusDiv2, classes: oracleArray));

            lifeChannelHarm.ReplaceResourceLogic(resource);
            lifeChannelHarm.ReplaceContextRankConfig(Helpers.CreateContextRankConfig(
                ContextRankBaseValueType.ClassLevel, ContextRankProgression.OnePlusDiv2, classes: oracleArray));

            feat.SetComponents(resource.CreateAddAbilityResource(),
                lifeChannel.CreateAddFact(), lifeChannelHarm.CreateAddFact());

            // Add Extra Channel, fix Selective Channel
            var extraChannel = library.CopyAndAdd<BlueprintFeature>("cd9f19775bd9d3343a31a065e93f0c47",
                "ExtraChannelOracle", "670d560ed7fe4329b2d311eba3600949");
            extraChannel.SetName("额外导能 (先知)");
            extraChannel.SetComponents(feat.PrerequisiteFeature(), resource.CreateIncreaseResourceAmount(2));
            library.AddFeats(extraChannel);

            var selectiveChannel = library.Get<BlueprintFeature>("fd30c69417b434d47b6b03b9c1f568ff");
            selectiveChannel.AddComponent(feat.PrerequisiteFeature(true));
            return feat;
        }

        static BlueprintFeature CreateEnergyBody()
        {
            var elementalBodyIIFireBuff = library.Get<BlueprintBuff>("103a680886ba18742a40b840c3b237f6");
            var auraOfHealingEffectBuff = library.Get<BlueprintBuff>("8960038b7e7fbcc46897ca86ce70bae4");

            var feat = Helpers.CreateFeature("MysteryLifeEnergyBody", "能量盈身",
                "以一个标准动作，你可以将你的身体转化为纯净的生命之力，外观有如一个散发着白金火焰的火元素。在这个型态下，你获得元素亚种，同时散发出一圈温暖，舒适的光环，这可以将你10尺内的光照等级提升一级，最多达到正常光照。任何不死生物用它的身躯或手持武器攻击你时，将受到“1d6+先知等级”点正能量伤害。使用长触及武器攻击你的不死生物将不会受到伤害。若你擒抱或用天生武器攻击不死生物，你可以用此伤害取代正常伤害。每轮1次，当你通过一个活体战友的方格，或着当活体战友通过你的方格时，他将恢复“1d6+先知等级”的生命值，你可以用一个移动等效动作对自己造成这个效果。当生物通过你的方格时，你可以选择要不要治疗对方。你可以用一个自由动作变回原形。你每天可以使用这个状态的轮数相当于你的先知等级。",
                "af6f1094822c4a34b0c81270a6fe281b",
                Helpers.GetIcon("4093d5a0eb5cae94e909eb1e0e1a6b36"), // remove disease
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "726f7ebf8c7447eba849f22b86390b08", null);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            var auraOfHealingArea = library.Get<BlueprintAbilityAreaEffect>("be47154a20220f64f9bea767587e700a");
            var cureLightWounds = library.Get<BlueprintAbility>("47808d23c67033d4bbab86a1070fd62f");
            var auraArea = library.CopyAndAdd(auraOfHealingArea, $"{feat.name}Area", "e3b2913bb6e84eb393209afb9b9a99c6");
            auraArea.Size = 5.Feet();
            auraArea.SetComponents(
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, classes: oracleArray),
                Helpers.CreateAreaEffectRunAction(round: Helpers.CreateConditional(
                    // Heal allies in area 1d6 + 1hp per caster level
                    Helpers.Create<ContextConditionIsAlly>(),
                    new GameAction[] {
                        Helpers.Create<ContextActionSpawnFx>(c =>
                            c.PrefabLink = cureLightWounds.GetComponent<AbilitySpawnFx>().PrefabLink),
                        Helpers.Create<ContextActionHealTarget>(c =>
                            c.Value = DiceType.D6.CreateContextDiceValue(1, Helpers.CreateContextValueRank()))
                    })));

            var polymorph = UnityEngine.Object.Instantiate(elementalBodyIIFireBuff.GetComponent<Polymorph>());
            polymorph.StrengthBonus = polymorph.DexterityBonus = polymorph.ConstitutionBonus = polymorph.NaturalArmor = 0;

            // Elemental subtype: immunity to bleed, paralysis, poison, sleep effects, and stunning.
            // Not subject to critical hits or flanking. Does not take additional damage from precision-based attacks, such as sneak attack.
            var immunities = SpellDescriptor.Sleep | SpellDescriptor.Paralysis | SpellDescriptor.Poison | SpellDescriptor.Bleed | SpellDescriptor.Stun;

            var undeadType = library.Get<BlueprintFeature>("734a29b693e9ec346ba2951b27987e33");
            var aasimarHaloBuff = library.Get<BlueprintBuff>("0b1c9d2964b042e4aadf1616f653eb95");
            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "b2fed2fbbb654cf7858d1fcb626e4f2d", feat.Icon, aasimarHaloBuff.FxOnStart,
                (SpellDescriptor.Polymorph | SpellDescriptor.RestoreHP | SpellDescriptor.Cure).CreateSpellDescriptor(),
                polymorph,
                AddMechanicsFeature.MechanicsFeatureType.NaturalSpell.CreateAddMechanics(),
                elementalBodyIIFireBuff.GetComponent<ReplaceAsksList>(),
                elementalBodyIIFireBuff.GetComponent<BuffMovementSpeed>(),
                UnitCondition.Sleeping.CreateImmunity(),
                UnitCondition.Paralyzed.CreateImmunity(),
                immunities.CreateBuffImmunity(),
                immunities.CreateSpellImmunity(),
                Helpers.Create<AddImmunityToCriticalHits>(),
                Helpers.Create<AddImmunityToPrecisionDamage>(),
                AddMechanicsFeature.MechanicsFeatureType.CannotBeFlanked.CreateAddMechanics(),
                Helpers.Create<AddAreaEffect>(a => a.AreaEffect = auraArea),
                Helpers.Create<AddTargetAttackRollTrigger>(a =>
                {
                    a.OnlyHit = true;
                    a.OnlyMelee = true;
                    a.NotReach = true;
                    a.ActionOnSelf = Helpers.CreateActionList();
                    a.ActionsOnAttacker = Helpers.CreateActionList(Helpers.CreateConditional(
                        // deal 1d6 + 1 per caster level to undead.
                        undeadType.CreateConditionHasFact(),
                        Helpers.CreateActionDealDamage(DamageEnergyType.PositiveEnergy,
                            DiceType.D6.CreateContextDiceValue(1, Helpers.CreateContextValueRank()))));
                }));

            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "6533576daa7944aab01b34b46b8f92d6", feat.Icon, buff, AbilityActivationType.WithUnitCommand,
                CommandType.Standard, null, resource.CreateActivatableResourceLogic(ResourceSpendType.NewRound));

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateEnhancedCures()
        {
            ContextRankConfig_GetValue_Patch.Apply();
            return enhancedCures = Helpers.CreateFeature("MysteryLifeEnhancedCures", "强力治疗",
                "当你施展一个治疗法术的时候，以你的先知等级，而不是法术提供的上限来决定额外的治疗加值。例如一个11级的先知施展“治疗轻伤”的时候，他将治疗“1d8+11”点伤害，而非“1d8+5”点。",
                "111a339509c140b2818877f538351bca",
                Helpers.GetIcon("3361c5df793b4c8448756146a88026ad"), // cure serious wounds
                FeatureGroup.None);
        }

        static BlueprintFeature CreateHealingHands()
        {
            var treatAfflictions = library.Get<BlueprintAbility>("4843cb4c23951f54290c5149a4907f54"); // LoreReligionUseAbility
            var feat = Helpers.CreateFeature("MysteryLifeHealingHands", "治愈之手",
                $"你在治疗检定中获得+4加值，你可以以迅捷动作使用 {treatAfflictions.Name} 。",
                "72138f6a0753498aaa6b5134f61e88ab",
                treatAfflictions.Icon,
                FeatureGroup.None,
                Helpers.Create<HealingHandsLogic>());
            return feat;
        }

        static BlueprintFeature CreateLifeLink()
        {
            var feat = Helpers.CreateFeature("MysteryLifeLink", "生命连接",
                "以一个标准动作，你可以在你与一个生物之间建立生命连结。每轮开始的时候，如果该生物的生命值比最大生命值低5点或以上，该生物恢复5点生命值而你受到5点伤害。 " +
                "你可以激活每先知等级1个额外链接。这个链接持续到你或该生物死亡；当你与该生物的距离超过中距，或你用直觉动作切断也能使连结结束。若你同时保有多条连结，你可以用同一个直觉动作将其切断。",
                "1c493aa458b94e13b6cd727b492d6cb4",
                Helpers.GetIcon("f8bce986adfc88544a42bf4ab7ae75b2"), // remove paralysis
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", feat.Name, "", "b51cb73f1d9d4381be54efe2ce10f5b5", null);
            resource.SetIncreasedByLevel(0, 1, oracleArray);

            var cureLightWounds = library.Get<BlueprintAbility>("47808d23c67033d4bbab86a1070fd62f");

            var removeBuff = Helpers.Create<ContextActionRemoveBuff>();
            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "66ec04bf0853419fa5dd742dba10ea30", feat.Icon, null,
                (SpellDescriptor.RestoreHP | SpellDescriptor.Cure).CreateSpellDescriptor(),
                Helpers.CreateAddFactContextActions(
                    deactivated: Helpers.Create<ContextRestoreResource>(c => c.Resource = resource),
                    newRound: Helpers.CreateConditional(
                        Helpers.Create<ContextConditionDistanceToTarget>(c => c.DistanceGreater = 40.Feet()),
                        removeBuff,
                        Helpers.CreateConditional(
                            Helpers.Create<ContextConditionHasDamage>(),
                            new GameAction[] {
                                Helpers.Create<ContextActionSpawnFx>(c =>
                                    c.PrefabLink = cureLightWounds.GetComponent<AbilitySpawnFx>().PrefabLink),
                                Helpers.Create<ContextActionTransferDamageToCaster>(c => c.Value = 5)
                            }))));
            buff.SetBuffFlags(BuffFlags.RemoveOnRest);
            removeBuff.Buff = buff;

            var linkAbility = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "3d1e78466db341b285fb3d97ce408a0d", feat.Icon,
                AbilityType.Supernatural, CommandType.Standard, AbilityRange.Medium, "", "",
                resource.CreateResourceLogic(),
                Helpers.CreateRunActions(buff.CreateApplyBuff(Helpers.CreateContextDuration(),
                    fromSpell: false, dispellable: false, permanent: true)));
            linkAbility.CanTargetFriends = true;
            linkAbility.EffectOnAlly = AbilityEffectOnUnit.Helpful;

            var dismissAbility = library.CopyAndAdd(linkAbility, $"{feat.name}Dismiss", "9cadee2d79bd436596296704a5637578");
            dismissAbility.SetName($"End {feat.Name}");
            dismissAbility.ActionType = CommandType.Free;
            dismissAbility.SetComponents(
                Helpers.Create<AbilityTargetHasFact>(a => a.CheckedFacts = new BlueprintUnitFact[] { buff }),
                ResourceIsFullChecker.Create(resource, not: true),
                Helpers.CreateRunActions(removeBuff));

            feat.SetComponents(resource.CreateAddAbilityResource(),
                linkAbility.CreateAddFact(), dismissAbility.CreateAddFact());
            return lifeLink = feat;
        }

        static BlueprintFeature CreateLifeSense()
        {
            return Helpers.CreateFeature("MysteryLifeSense", "生命感知",
                "你可以感觉到30尺内的活物，如同你拥有“盲视”能力。你必须达到11级才有办法选择这个启示。",
                "9a9f7afcc70742fca888ab73f73996d4",
                Helpers.GetIcon("4cf3d0fae3239ec478f51e86f49161cb"), // true seeing
                FeatureGroup.None,
                Helpers.Create<Blindsense>(b => { b.Range = 30.Feet(); b.Blindsight = true; }),
                oracle.PrerequisiteClassLevel(11));
        }

        static BlueprintFeature CreateCombatHealer() => BattleMystery.CreateCombatHealer();

        static BlueprintFeature CreateDelayAffliction()
        {
            var delayPoisonBuff = library.Get<BlueprintBuff>("51ebd62ee464b1446bb01fa1e214942f");
            var feat = Helpers.CreateFeature("MysteryLifeDelayAffliction", "推迟痛苦",
                "每日1次，以一个直觉动作，当你在对抗毒素或疾病的豁免检定失败的时候，你可以忽视此不良效果的影响每先知等级1小时。7级和15级你可以得到1次额外的使用次数。",
                "42f849d6508949658931e4ed9bca77a8",
                delayPoisonBuff.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "a109ac8d531c409ea46a3b4dab23d87b", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 7, 1, 8, 1, 0, 0, oracleArray);

            var immuneToDisease = Helpers.CreateBuff($"{feat.name}DelayDiseaseBuff", $"{feat.Name} — Disease",
                feat.Description, "4253ce04b612481683755a71e9b09c25", feat.Icon, null,
                SpellDescriptor.Disease.CreateSpellImmunity(),
                SpellDescriptor.Disease.CreateBuffImmunity());

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "074fdaa6cd5d4d61bdbfae8519466120", feat.Icon, null,
                Helpers.CreateContextRankConfig(),
                RunActionOnFailedSave.Create(resource, SpellDescriptor.Disease,
                    immuneToDisease.CreateApplyBuff(Helpers.CreateContextDuration(rate: DurationRate.Hours),
                        fromSpell: false, dispellable: false)),
                RunActionOnFailedSave.Create(resource, SpellDescriptor.Poison,
                    delayPoisonBuff.CreateApplyBuff(Helpers.CreateContextDuration(rate: DurationRate.Hours),
                        fromSpell: false, dispellable: false)));

            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "b77b658e1c0a4c129d27ebb1fd02a25c", feat.Icon, buff, AbilityActivationType.Immediately,
                CommandType.Free, null, resource.CreateActivatableResourceLogic(ResourceSpendType.Never));
            ability.IsOnByDefault = true;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateSafeCuring()
        {
            return Helpers.CreateFeature("MysteryLifeSafeCuring", "安全治疗",
                "当你使用“治疗”法术治疗伤害的时候，你不会引起对施法的藉机攻击。",
                "fe4e347238a544ec94be7a665d6b7910",
                Helpers.GetIcon("7aa83ee3526a946419561d8d1aa09e75"), // arcane combat casting
                FeatureGroup.None,
                ImmuneToAttackOfOpportunityForSpells.Create(SpellDescriptor.RestoreHP));
        }

        static BlueprintFeature CreateSpiritBoost()
        {
            var falseLifeBuff = library.Get<BlueprintBuff>("0fdb3cca6744fd94b9436459e6d9b947");
            var feat = Helpers.CreateFeature("MysteryLifeSpiritBoost", "灵性提升",
                "如果你的治疗法术把目标的生命提升到最大值以上的话，多出的治疗量将以临时生命值的方式保存，持续时间最多每先知等级1轮（最大临时生命值等同于你的先知等级）。",
                "769eba54ba5f40548eab67f0c5aff6e4",
                falseLifeBuff.Icon, FeatureGroup.None);

            var buff = library.CopyAndAdd(falseLifeBuff, $"{feat.name}Buff", "9a163797dcfa4d87a2162f41079491be");
            buff.SetComponents(
                buff.GetComponent<TemporaryHitPointsFromAbilityValue>(),
                buff.GetComponent<SpellDescriptorComponent>());

            feat.SetComponents(MysteryLifeSpiritBoostLogic.Create(buff));
            return feat;
        }

        static BlueprintFeature CreateFinalRevelation()
        {
            var immunities = SpellDescriptor.Bleed | SpellDescriptor.Death | SpellDescriptor.Exhausted | SpellDescriptor.Fatigue | SpellDescriptor.Nauseated | SpellDescriptor.Sickened;

            var feat = Helpers.CreateFeature("MysteryLifeFinalRevelation", "最终启示",
                "20级时，你化身为完整纯粹的生命能量。 " +
                "你免疫出血、即死效果、力竭、疲乏、恶心、反胃和负向等级，" +
                "而且任何属性伤害和属性吸取能力没办法将你的属性降到1以下。",
                "88c41e041d3d4e68b24e4d0d04ed51a0",
                Helpers.GetIcon("be2062d6d85f4634ea4f26e9e858c3b8"), // cleanse
                FeatureGroup.None,
                immunities.CreateBuffImmunity(),
                immunities.CreateSpellImmunity(),
                UnitCondition.Exhausted.CreateImmunity(),
                UnitCondition.Fatigued.CreateImmunity(),
                UnitCondition.Nauseated.CreateImmunity(),
                UnitCondition.Paralyzed.CreateImmunity(),
                UnitCondition.Sickened.CreateImmunity(),
                Helpers.Create<AddImmunityToEnergyDrain>(),
                Helpers.Create<MysteryLifeFinalRevelationLogic>());

            return feat;
        }
    }

    public class ImmuneToAttackOfOpportunityForSpells : RuleInitiatorLogicComponent<RuleCalculateAbilityParams>
    {
        public SpellDescriptorWrapper Descriptor;

        public static ImmuneToAttackOfOpportunityForSpells Create(SpellDescriptor descriptor = SpellDescriptor.None)
        {
            var i = Helpers.Create<ImmuneToAttackOfOpportunityForSpells>();
            i.Descriptor = descriptor;
            return i;
        }

        public override void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            if (evt.Spell.Type == AbilityType.Spell &&
                (Descriptor == SpellDescriptor.None || (evt.Spell.SpellDescriptor & Descriptor) != 0))
            {
                evt.AddBonusConcentration(1000);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }
    }

    public class MysteryLifeSpiritBoostLogic : RuleInitiatorLogicComponent<RuleHealDamage>
    {
        public BlueprintBuff HitPointBuff;
        public AbilitySharedValue HitPointValueToUpdate;

        public static MysteryLifeSpiritBoostLogic Create(BlueprintBuff buff, AbilitySharedValue variable = AbilitySharedValue.Heal)
        {
            var m = Helpers.Create<MysteryLifeSpiritBoostLogic>();
            m.HitPointBuff = buff;
            m.HitPointValueToUpdate = variable;
            return m;
        }

        public override void OnEventAboutToTrigger(RuleHealDamage evt)
        {
            var context = Helpers.GetMechanicsContext()?.SourceAbilityContext;
            var spell = context?.SourceAbility;
            // According to the ability description, it must be from a healing spell
            // (not a supernatural ability like Energy Body, Channel Energy, etc.)
            Log.Append($"Spirit Boost: spell {spell?.name}, type {spell?.Type}, descriptor {spell.SpellDescriptor}");

            if (spell != null && spell.Type == AbilityType.Spell && !(evt is RuleHealDamageWithOverflow) &&
                (spell.SpellDescriptor & SpellDescriptor.RestoreHP) != 0)
            {
                // Disable the original heal roll.
                // We need to simulate it so we can get the temporary HP amount.
                evt.Modifier = 0;

                // Trigger the heal and collect the overflow heal amount, if any.
                int amount = Rulebook.Trigger(new RuleHealDamageWithOverflow(evt)).OverflowHealing;
                if (amount > 0)
                {
                    var oracleLevel = Owner.Progression.GetClassLevel(OracleClass.oracle);
                    amount = Math.Min(amount, oracleLevel);

                    context[HitPointValueToUpdate] = amount;
                    var duration = oracleLevel.Rounds().Seconds;
                    Log.Write($"Spirit Boost: apply {amount} temporary HP to {evt.Target.CharacterName} for {duration}, spell was {spell.name}");
                    evt.Target.Buffs.AddBuff(HitPointBuff, context, duration);
                }
            }
        }

        public override void OnEventDidTrigger(RuleHealDamage evt) { }
    }

    class RuleHealDamageWithOverflow : RuleHealDamage
    {
        public int OverflowHealing;

        public RuleHealDamageWithOverflow(RuleHealDamage rule)
            : base(rule.Initiator, rule.Target, rule.HealFormula, rule.Bonus)
        { }

        public override void OnTrigger(RulebookEventContext context)
        {
            float multiplier = Modifier ?? 1f;
            int healAmount = Math.Max(0, (int)((Dice.D(HealFormula) + Bonus) * multiplier));
            var value = Math.Min(healAmount, Target.Damage);
            setValue(this, value);
            Target.Damage -= value;
            Log.Write($"{GetType().Name}: healed {Target.CharacterName} for {Value} HP ({value}), damage now at: {Target.Damage}");
            UnitPartDualCompanion.HandleHealing(Target, healAmount);
            EventBus.RaiseEvent((IHealingHandler h) => h.HandleHealing(Initiator, Target, Value));
            var overflow = healAmount - Target.Damage;
            if (overflow > 0) OverflowHealing = overflow;
        }

        static readonly FastSetter setValue = Helpers.CreateSetter<RuleHealDamage>("Value");
    }

    public class HealingHandsLogic : RuleInitiatorLogicComponent<RuleCalculateAbilityParams>, IInitiatorRulebookHandler<RuleDispelMagic>
    {
        public override void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            evt.AddMetamagic(Metamagic.Quicken);
        }

        public void OnEventAboutToTrigger(RuleDispelMagic evt)
        {
            if (evt.Check == RuleDispelMagic.CheckType.SkillDC && evt.Skill == StatType.SkillLoreReligion)
            {
                evt.AddTemporaryModifier(Owner.Stats.SkillLoreReligion.AddModifier(4, this, ModifierDescriptor.UntypedStackable));
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }

        public void OnEventDidTrigger(RuleDispelMagic evt) { }
    }

    public class RunActionOnFailedSave : BuffLogic, IInitiatorRulebookHandler<RuleSavingThrow>
    {
        public BlueprintAbilityResource Resource;
        public SpellDescriptorWrapper Descriptor;
        public ActionList OnFailedSave;

        public static RunActionOnFailedSave Create(BlueprintAbilityResource resource, SpellDescriptor descriptor, GameAction failedSave)
        {
            var i = Helpers.Create<RunActionOnFailedSave>();
            i.Resource = resource;
            i.Descriptor = descriptor;
            i.OnFailedSave = Helpers.CreateActionList(failedSave);
            return i;
        }

        public void OnEventAboutToTrigger(RuleSavingThrow evt) { }

        public void OnEventDidTrigger(RuleSavingThrow evt)
        {
            if (!evt.IsPassed && evt.Reason.Context?.SpellDescriptor == Descriptor)
            {
                Buff.RunActionInContext(OnFailedSave, Owner.Unit);
                Owner.Resources.Spend(Resource, 1);
                if (Owner.Resources.GetResourceAmount(Resource) == 0) Buff.Remove();
            }
        }
    }

    public class ContextConditionHasDamage : ContextCondition
    {
        protected override bool CheckCondition() => Target.Unit?.Damage > 0;

        protected override string GetConditionCaption() => "Whether the target is damaged";
    }

    public class ContextActionTransferDamageToCaster : BuffAction
    {
        public ContextValue Value;

        public override string GetCaption() => $"Transfer {Value} of hit point damage to caster.";

        public override void RunAction()
        {
            int value = Value.Calculate(Context);
            var caster = Context.MaybeCaster;
            // Prevent the damage transfer from killing the caster, and automatically end the Life Link.
            // (Makes it easier to manage, since this is a CRPG.)
            if (caster.HPLeft < value)
            {
                Log.Write($"Can't transfer {value} damage, caster only has {caster.HPLeft} HP.");
                Buff.RemoveAfterDelay();
                return;
            }
            var target = Buff.Owner.Unit;
            var rule = Context.TriggerRule(new RuleHealDamage(caster, target, DiceFormula.Zero, value));
            if (rule.Value > 0)
            {
                Context.TriggerRule(new RuleDealDamage(caster, caster, new DamageBundle(
                    new EnergyDamage(new DiceFormula(rule.Value, DiceType.One), DamageEnergyType.Magic))));
            }
            Log.Write($"Transfer {rule.Value} damage from {target.CharacterName} to {caster.CharacterName}");
        }
    }

    public class MysteryLifeFinalRevelationLogic : RuleTargetLogicComponent<RuleDealStatDamage>
    {
        bool wasMarkedForDeath;

        public override void OnEventAboutToTrigger(RuleDealStatDamage evt)
        {
            wasMarkedForDeath = evt.Target.Descriptor.State.MarkedForDeath;
        }

        public override void OnEventDidTrigger(RuleDealStatDamage evt)
        {
            var stat = evt.Stat;
            var value = stat.ModifiedValueRaw;
            if (value < 1)
            {
                evt.Target.Descriptor.State.MarkedForDeath = wasMarkedForDeath;
                var adjust = 1 - value; // bring the value back to 1.
                if (evt.IsDrain)
                {
                    stat.Drain -= adjust;
                }
                else
                {
                    stat.Damage -= adjust;
                }
            }
        }
    }


    public class ContextRestoreResource : ContextAction
    {
        public BlueprintAbilityResource Resource;

        public override string GetCaption() => "Restore resourse";

        public override void RunAction()
        {
            var caster = Context.MaybeCaster;
            if (caster == null)
            {
                UberDebug.LogError("Caster is missing");
                return;
            }
            caster.Descriptor.Resources.Restore(Resource, 1);
        }
    }


    [AllowedOn(typeof(BlueprintAbility))]
    public class ResourceIsFullChecker : BlueprintComponent, IAbilityCasterChecker
    {
        public BlueprintAbilityResource Resource;
        public bool Not;

        public static ResourceIsFullChecker Create(BlueprintAbilityResource resource, bool not)
        {
            var r = Helpers.Create<ResourceIsFullChecker>();
            r.Resource = resource;
            r.Not = not;
            return r;
        }

        public bool CorrectCaster(UnitEntityData caster)
        {
            var available = caster.Descriptor.Resources.GetResourceAmount(Resource);
            var max = Resource.GetMaxAmount(caster.Descriptor);
            return (available == max) != Not;
        }

        public string GetReason() => $"{Resource.Name} is not active on any targets.";
    }
}
