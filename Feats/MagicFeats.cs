// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;
using Newtonsoft.Json;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class MagicFeats
    {
        static LibraryScriptableObject library => Main.library;

        static BlueprintCharacterClass magus;

        internal static void Load()
        {
            magus = library.Get<BlueprintCharacterClass>("45a4607686d96a1498891b3286121780");

            // Load metamagic feats
            var metamagicFeats = SafeLoad(MetamagicFeats.CreateMetamagicFeats, "Metamagic feats")?.ToArray();
            if (metamagicFeats == null) metamagicFeats = Array.Empty<BlueprintFeature>();
            var feats = metamagicFeats.ToList();

            // Add metamagics to Magus/Sorcerer bonus feat list.
            library.AddFeats( /*SorcererBonusFeat*/ "d6dd06f454b34014ab0903cb1ed2ade3", metamagicFeats);
            library.AddFeats(Helpers.magusFeatSelection, metamagicFeats);

            SafeAddToList(CreateOppositionResearch, feats, "禁制学派研究");

            // Add metamagics and Opposition Research to Wizard bonus feat list.
            library.AddFeats( /*WizardFeatSelection*/ "8c3102c2ff3b69444b139a98521a4899", feats.ToArray());

            SafeAddToList(CreateMagesTattoo, feats, "法师纹身");
            SafeAddToList(() => CreateSpellPerfection(metamagicFeats), feats, "完美法术");

            // Magus Arcanas, Extra Magus Arcana
            SafeAddToList(LoadMagusArcanas, feats, "魔战士奥秘");

            Main.SafeLoad(LoadDervishDance, "优雅之舞");

            SafeAddToList(CreateFeyFoundling, feats, "妖精遗孤");

            // Add all feats (including metamagic, wizard discoveries) to general feats.
            library.AddFeats(feats.ToArray());


            Main.SafeLoad(FixSpellSpecialization, "Spell Specialization");

            Main.SafeLoad(AddSpellScrolls, "Scrolls for new spells");
        }

        internal static T SafeLoad<T>(Func<T> load, String name) => Main.SafeLoad(load, name);

        internal static void SafeAddToList<T>(Func<T> load, List<T> list, String name)
        {
            var result = SafeLoad(load, name);
            if (result != null) list.Add(result);
        }

        static BlueprintFeature CreateFeyFoundling()
        {
            var feat = Helpers.CreateFeature("FeyFoundling", "妖精遗孤",
                "当你在野外被找到时你还是个孩子.\n无论何时，当你受到魔法治疗时, 你受到的治愈效果在每次投骰时都额外+2， 你的所有豁免检定在对抗死亡效果时提升2点. 不幸的是, 当你受到寒铁武器的伤害时，每个伤害骰都额外+1 (尽管你可以使用寒铁武器并且不会有明显不适).",
                "0659556638b04ecc85e069e050751bfa",
                Helpers.GetIcon("e8445256abbdc45488c2d90373f7dae8"),
                FeatureGroup.Feat,
                Helpers.Create<SavingThrowBonusAgainstDescriptor>(s => { s.SpellDescriptor = SpellDescriptor.Death; s.ModifierDescriptor = ModifierDescriptor.Feat; }),
                Helpers.Create<FeyFoundlingLogic>(),
                PrerequisiteCharacterLevelExact.Create(1));
            return feat;
        }

        static BlueprintFeature LoadMagusArcanas()
        {
            var arcanas = new List<BlueprintFeature> {
                CreateSpellBlending(),
                CreateMagusFamiliar()
            };

            var empowerSpell = library.Get<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e");
            var maximizeSpell = library.Get<BlueprintFeature>("7f2b282626862e345935bbea5e66424b");
            var quickenSpell = library.Get<BlueprintFeature>("ef7ece7bb5bb66a41b256976b27f424e");

            var metamagic = new BlueprintFeature[] { empowerSpell, maximizeSpell, quickenSpell };
            var requiredLevels = new int[] { 6, 12, 15 };
            arcanas.AddRange(metamagic.Zip(requiredLevels, CreateMagusArcanaMetamagic));

            var magusArcanas = library.Get<BlueprintFeatureSelection>("e9dc4dfc73eaaf94aae27e0ed6cc9ada");
            magusArcanas.SetFeatures(magusArcanas.AllFeatures.AddToArray(arcanas));
            return CreateExtraArcana(magusArcanas);
        }

        static BlueprintFeature CreateMagusArcanaMetamagic(BlueprintFeature metamagicFeat, int requiredLevel)
        {
            var metamagic = metamagicFeat.GetComponent<AddMetamagicFeat>().Metamagic;
            var name = metamagic.ToString();
            name += name.EndsWith("e") ? "d" : "ed";
            var feat = Helpers.CreateFeature($"Magus{name}Magic", $"{name} 奥秘",
                $"魔战士每天可以释放一个法术，就像它被 {metamagic} 超魔专长调整过一样. " +
                (metamagic == Metamagic.Quicken
                    ? "这不会增加法术的环级."
                    : "这不会增加法术的施法时间和环级."),
                Helpers.MergeIds(metamagicFeat.AssetGuid, "65768d69b6b84954b3d6a1d1dc265cf8"),
                metamagicFeat.Icon,
                FeatureGroup.MagusArcana);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "",
                Helpers.MergeIds(metamagicFeat.AssetGuid, "d7183ba98b094f8295181c9946319773"), null);
            resource.SetFixedResource(1);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                Helpers.MergeIds(metamagicFeat.AssetGuid, "e1b0f16092b7411aa7789cc598e232da"), feat.Icon, null,
                Helpers.Create<MetamagicOnNextSpellFixed>(m => m.Metamagic = metamagic));

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                Helpers.MergeIds(metamagicFeat.AssetGuid, "2c79573d82754a178b7767d9c51bd247"), feat.Icon,
                AbilityType.Supernatural, CommandType.Free, AbilityRange.Personal, "", "",
                resource.CreateResourceLogic(),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(1), permanent: true, fromSpell: false, dispellable: false, toCaster: true)));

            feat.SetComponents(magus.PrerequisiteClassLevel(requiredLevel),
                resource.CreateAddAbilityResource(), ability.CreateAddFact());

            return feat;
        }

        static BlueprintFeatureSelection CreateExtraArcana(BlueprintFeatureSelection magusArcanas)
        {
            var feat = Helpers.CreateFeatureSelection("ExtraArcanaSelection", "额外奥秘",
                "你已经研究出了一个新的魔战士奥秘。你获得一个额外的魔战士奥秘，你必须满足这个奥秘的所有先决条件。\n" +
                "特殊: 你可以多次选择这个专长，它的效果叠加。你每次选择这个专长都要选择一个新奥秘。",
                "bace31a97ed141d9b11cc5dabacb5b88",
                Helpers.GetIcon("cd9f19775bd9d3343a31a065e93f0c47"), // extra channel
                FeatureGroup.Feat,
                magusArcanas.PrerequisiteFeature());
            feat.SetFeatures(magusArcanas.AllFeatures);
            return feat;
        }

        static BlueprintFeatureSelection CreateMagusFamiliar()
        {
            var arcaneBondSelection = library.Get<BlueprintFeatureSelection>("03a1781486ba98043afddaabf6b7d8ff");
            var itemBondFeature = library.Get<BlueprintFeature>("2fb5e65bd57caa943b45ee32d825e9b9");

            var feat = Helpers.CreateFeatureSelection("MagusFamiliarSelection", "魔宠",
                "魔战士获得一个魔宠, 使用他的魔战士等级作为有效法师等级。这个魔宠遵循法师的魔宠规则。",
                "310ed9de256445cca2915c4c0bc16f0b", arcaneBondSelection.Icon, FeatureGroup.MagusArcana);
            feat.SetComponents(feat.PrerequisiteNoFeature());
            feat.SetFeatures(arcaneBondSelection.Features.Where(f => f != itemBondFeature));
            return feat;
        }

        static BlueprintFeature CreateSpellBlending()
        {
            var name = "SpellBlending";
            var feat = Helpers.CreateFeatureSelection($"{name}Selection", "法术混合",
                "当魔战士选择这个奥秘时，他可以向法术书中添加一个他可以施展的位于法师列表中的法术。 使用他的魔战士等级作为法师等级来决定施法者等级。 他也可以选择以这种方式添加两个法术, 但这两个法术必须至少比他能施展的最高环法术低一环。\n特殊: 魔战士可以多次选择这个奥秘。",
                "0a273cce57ed44bdb2b9f36270c23cb8",
                Helpers.GetIcon("55edf82380a1c8540af6c6037d34f322"), // elven magic
                FeatureGroup.MagusArcana);

            var pickOneSpell = Helpers.CreateParamSelection<SelectAnySpellAtComputedLevel>(
                $"{name}OneSpellSelection",
                $"{feat.Name} (单个法术)",
                feat.Description,
                "0744c1eca7084c18aef2230828680cc9",
                null,
                FeatureGroup.MagusArcana,
                Helpers.wizardSpellList.CreateLearnSpell(magus));
            pickOneSpell.SpellList = Helpers.wizardSpellList;
            pickOneSpell.SpellcasterClass = magus;

            var pickTwoSpells = Helpers.CreateFeature($"{name}TwoSpellProgression",
                $"{feat.Name} (两个法术)",
                feat.Description,
                "1fa334799e9a4a169fa53d154af86363",
                null,
                FeatureGroup.MagusArcana,
                PrerequisiteCasterSpellLevel.Create(magus, 2));

            var pickSpellChoice1 = CreateSpellBlendingSelection(magus, feat, 1, "98aa453c00304b3e990d08b709e2fd24");
            var pickSpellChoice2 = CreateSpellBlendingSelection(magus, feat, 2, "cc40113b6e884c99b4141a25b7825aa8");
            SelectFeature_Apply_Patch.onApplyFeature.Add(pickTwoSpells, (state, unit) =>
            {
                var choice = pickSpellChoice1;
                state.AddSelection(null, choice, choice, 1);
                choice = pickSpellChoice2;
                state.AddSelection(null, choice, choice, 1);
            });

            feat.SetFeatures(pickOneSpell, pickTwoSpells, UndoSelection.Feature.Value);
            return feat;
        }

        static BlueprintFeatureSelection CreateSpellBlendingSelection(
            BlueprintCharacterClass magus, BlueprintFeature feat, int index, String assetId)
        {
            var pickSpell = Helpers.CreateFeatureSelection($"{feat.name}Spell{index}",
                    feat.Name, feat.Description, assetId, feat.Icon,
                    FeatureGroup.MagusArcana);
            var wizardList = Helpers.wizardSpellList;
            Traits.FillSpellSelection(pickSpell, 1, 6, wizardList, (level) => new BlueprintComponent[] {
                PrerequisiteCasterSpellLevel.Create(magus, level + 1),
                Helpers.wizardSpellList.CreateLearnSpell(magus, level)
            }, magus);
            return pickSpell;
        }

        static void LoadDervishDance()
        {
            var slashingGrace = library.Get<BlueprintParametrizedFeature>("697d64669eb2c0543abb9c9b07998a38");
            var weaponFinesse = library.Get<BlueprintFeature>("90e54424d682d104ab36436bd527af09");

            var dervishDance = Helpers.CreateFeature("DervishDance", "优雅之舞",
                "当你单手持用弯刀时，你可以在你的近战攻击命中和伤害投骰中用你的敏捷调整值取代力量调整值。你可以将弯刀视作单手穿刺武器来使用所有需要这类武器的专长和职业能力 (例如决斗家的精确打击能力)。弯刀必须符合你的体型。如果你另一只手持用武器或盾牌，此专长无效。",
                "7d0bb2ade9344cae833c5bbe66bf0460",
                slashingGrace.Icon,
                FeatureGroup.Feat,
                weaponFinesse.PrerequisiteFeature(),
                Helpers.PrerequisiteStatValue(StatType.Dexterity, 13),
                Helpers.PrerequisiteStatValue(StatType.SkillPersuasion, 2),
                Helpers.Create<PrerequisiteProficiency>(p =>
                {
                    p.WeaponProficiencies = new WeaponCategory[] { WeaponCategory.Scimitar };
                    p.ArmorProficiencies = new Kingmaker.Blueprints.Items.Armors.ArmorProficiencyGroup[0];
                }),
                Helpers.Create<DamageGraceForWeapon>(d => d.Category = WeaponCategory.Scimitar));
            library.AddCombatFeats(dervishDance);
        }

        internal static BlueprintParametrizedFeature spellFocus, spellFocusGreater;
        internal static BlueprintProgression spellSpecialization;
        internal static BlueprintParametrizedFeature magesTattoo;
        internal static BlueprintFeature spellPenetration, spellPenetrationGreater;
        internal static BlueprintFeatureSelection elementalFocus, elementalFocusGreater;

        static BlueprintParametrizedFeature CreateMagesTattoo()
        {
            spellFocus = library.Get<BlueprintParametrizedFeature>("16fa59cc9a72a6043b566b49184f53fe");
            spellFocusGreater = (library.Get<BlueprintParametrizedFeature>("5b04b45b228461c43bad768eb0f7c7bf"));
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            magesTattoo = Helpers.CreateParametrizedFeature("MagesTattooSelection",
                "法师纹身",
                "选择一个你有法术专攻的法术学派，你所施展的此学派法术获得+1施法者等级。",
                "8004aabdc67145c5b0613b7580d77da1",
                spellFocusGreater.Icon,
                FeatureGroup.Feat,
                FeatureParameterType.SpellSchool,
                spellFocus.PrerequisiteFeature(),
                Helpers.Create<RecommendationRequiresSpellbook>(),
                Helpers.Create<CasterLevelForSchoolParameterized>(),
                Helpers.Create<RecommendationHasFeature>(r => r.Feature = spellFocus),
                noFeature);
            noFeature.Feature = magesTattoo;
            magesTattoo.Prerequisite = spellFocus;
            magesTattoo.Ranks = 1;
            return magesTattoo;
        }

        static BlueprintParametrizedFeature CreateSpellPerfection(BlueprintFeature[] newMetamagics)
        {
            var library = Main.library;
            var allMetamagics = newMetamagics.Concat((new String[] {
                "a1de1e4f92195b442adb946f0e2b9d4e",
                "f180e72e4a9cbaa4da8be9bc958132ef",
                "2f5d1e705c7967546b72ad8218ccf99c",
                "7f2b282626862e345935bbea5e66424b",
                "ef7ece7bb5bb66a41b256976b27f424e",
                "46fad72f54a33dc4692d3b62eca7bb78",
            }).Select(id => (BlueprintFeature)library.BlueprintsByAssetId[id])).ToArray();

            spellSpecialization = library.Get<BlueprintProgression>("fe9220cdc16e5f444a84d85d5fa8e3d5");
            spellPenetration = library.Get<BlueprintFeature>("ee7dc126939e4d9438357fbd5980d459");
            spellPenetrationGreater = library.Get<BlueprintFeature>("1978c3f91cfbbc24b9c9b0d017f4beec");
            elementalFocus = library.Get<BlueprintFeatureSelection>("bb24cc01319528849b09a3ae8eec0b31");
            elementalFocusGreater = library.Get<BlueprintFeatureSelection>("1c17446a3eb744f438488711b792ca4d");

            var spellSpecialization1 = library.Get<BlueprintParametrizedFeature>("f327a765a4353d04f872482ef3e48c35");
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var spellPerfection = Helpers.CreateParamSelection<KnownSpellSelection>("SpellPerfection",
                "完美法术",
                "选择一个你能够释放的法术，每次你释放此法术时，你可以在不影响施法时间和法术环级的情况下对其施加任何一个你所知晓的超魔专长，只要此法术的合计等级不超过九级（添加多个超魔时，环数调整最低的适用此专长）。此外，如果你有其他专长允许你对这个法术的任何一个方面添加一个加值（例如法术专攻，法术穿透，武器专攻-射线等），那么这些专长在这个法术上的效果加倍。",
                "82165fb15af34cbb9c0c2e6fb232b2fc",
                spellSpecialization.Icon,
                FeatureGroup.Feat,
                Helpers.PrerequisiteStatValue(StatType.SkillKnowledgeArcana, 15),
                Helpers.Create<PrerequisiteFeaturesFromList>(p => { p.Amount = 3; p.Features = allMetamagics; }),
                Helpers.Create<ReduceMetamagicCostForSpell>(r => r.OneMetamagicIsFree = true),
                Helpers.Create<SpellPerfectionDoubleFeatBonuses>(),
                noFeature);
            noFeature.Feature = spellPerfection;
            return spellPerfection;
        }

        static BlueprintFeatureSelection CreateOppositionResearch()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var wizardClass = Helpers.GetClass("ba34257984f4c41408ce1dc2004e342e");
            var spellFocusGreater = (library.Get<BlueprintFeature>("5b04b45b228461c43bad768eb0f7c7bf"));
            var oppositionResearch = Helpers.CreateFeatureSelection("OppositionResearchSelection",
                "禁制学派研究",
                "选择一个你的禁制学派，现在准备此学派的法术只需要一个合适等级的法术位而不是两个。并且你不再在制造此学派物品时承受-4惩e",
                "48eb4a47b01e4d088f763ff20824189e",
                spellFocusGreater.Icon,
                FeatureGroup.WizardFeat,
                Helpers.PrerequisiteClassLevel(wizardClass, 9),
                noFeature);
            noFeature.Feature = oppositionResearch;
            var schoolFeats = new List<BlueprintFeature>();
            var oppositionSchools = library.Get<BlueprintFeatureSelection>("6c29030e9fea36949877c43a6f94ff31");

            foreach (var school in EnumUtils.GetValues<SpellSchool>())
            {
                if (school == SpellSchool.None || school == SpellSchool.Universalist) continue;

                var oppositionFeat = oppositionSchools.AllFeatures.First(
                    f => f.GetComponent<AddOppositionSchool>()?.School == school);
                var specialistFeat = oppositionFeat.GetComponent<PrerequisiteNoFeature>().Feature;

                var schoolName = LocalizedTexts.Instance.SpellSchoolNames.GetText(school);
                schoolFeats.Add(Helpers.CreateFeature(oppositionResearch.name + school,
                    oppositionResearch.Name + $" — {schoolName}",
                    oppositionResearch.Description,
                    Helpers.MergeIds(oppositionResearch.AssetGuid, oppositionFeat.AssetGuid),
                    specialistFeat.Icon,
                    FeatureGroup.Feat,
                    oppositionFeat.PrerequisiteFeature(),
                    RemoveOppositionSchool.Create(wizardClass, school)));
            }
            oppositionResearch.Groups = new FeatureGroup[] { FeatureGroup.Feat, FeatureGroup.WizardFeat };
            oppositionResearch.SetFeatures(schoolFeats);
            return oppositionResearch;
        }

        static void FixSpellSpecialization()
        {
            // Fix Spell Specialization:
            // - should be able to select any (potentially known spell), not just those on the class spell list.
            // - should recognize Oracle as a caster class.
            // - should recognize new spells added by this mod.
            //
            // This also fixes other similar spell selection feats.
            Main.ApplyPatch(typeof(BlueprintParametrizedFeature_ExtractItemsFromSpellbooks_Patch), "Spell Specialization fix that allows Bloodline/Mystery spells to be picked");
            var spellSpecProgression = library.Get<BlueprintProgression>("fe9220cdc16e5f444a84d85d5fa8e3d5");
            spellSpecProgression.Classes = Helpers.classes.ToArray();

            var spellSpecSelection = library.Get<BlueprintFeatureSelection>("fe67bc3b04f1cd542b4df6e28b6e0ff5");

            var spellSelectors = spellSpecSelection.AllFeatures.OfType<BlueprintParametrizedFeature>().ToList();
            // Note: the NewArcana and Inquisitor selection use the same array (containing all spells).
            spellSelectors.AddRange(new string[] {
                "4a2e8388c2f0dd3478811d9c947bebfb", // BloodlineArcaneNewArcanaFeature
                "c66e61dea38f3d8479a54eabec20ac99", // BloodlineArcaneNewArcanaFeatureMagus
                "bcd757ac2aeef3c49b77e5af4e510956", // MysticTheurgeInquisitorLevelParametrized1
                "4869109802e135e45af20741f9056fd5", // MysticTheurgeInquisitorLevelParametrized2
                "e3a9ed781f9093341ac1073f59018e3f", // MysticTheurgeInquisitorLevelParametrized3
                "7668fd94a4f943e4f85ee025a0140434", // MysticTheurgeInquisitorLevelParametrized4
                "d3d8b837733879848b549189f02f535c", // MysticTheurgeInquisitorLevelParametrized5
                "0495474b37304054eaf016016d0002b4", // MysticTheurgeInquisitorLevelParametrized6
            }.Select(library.Get<BlueprintParametrizedFeature>));

            BlueprintScriptableObject[] allSpells = null;
            foreach (var choice in spellSelectors)
            {
                allSpells = allSpells ?? choice.BlueprintParameterVariants.AddToArray(Helpers.modSpells);
                choice.BlueprintParameterVariants = allSpells;
            }
        }

        static void AddSpellScrolls()
        {
            var scrollsByLevel = Helpers.modScrolls.GroupBy(s => s.SpellLevel).ToDictionary(g => g.Key, g => g.ToArray());
            var placed = new HashSet<int>();
            BlueprintScriptableObject lootTable = null;

            Helpers.AddNearSimilarLoot((item, count) =>
            {
                if (count > 90) return null;
                var scroll = item as BlueprintItemEquipmentUsable;
                if (scroll == null || scroll.Type != UsableItemType.Scroll) return null;
                var level = scroll.SpellLevel;
                if (!placed.Add(level)) return null;
                BlueprintItemEquipmentUsable[] scrolls;
                scrollsByLevel.TryGetValue(level, out scrolls);
                return scrolls;
            }, (loot) =>
            {
                if (!loot.name.Contains("Vendor")) return false;
                lootTable = loot;
                placed.Clear();
                return true;
            });
        }
    }

    public class ReduceMetamagicCostForSpell : ParametrizedFeatureComponent, IInitiatorRulebookHandler<RuleApplyMetamagic>
    {
        public int Reduction;

        public int MaxSpellLevel = 9;

        public bool OneMetamagicIsFree;

        public void OnEventAboutToTrigger(RuleApplyMetamagic evt)
        {
            var spell = (BlueprintAbility)Param.GetValueOrDefault().Blueprint;
            if (evt.Spell != spell && evt.Spell?.Parent != spell) return;
            var spellbook = evt.Spellbook;
            if (spellbook == null || spellbook.GetSpellLevel(spell) > MaxSpellLevel) {
                return;
            }
            Log.Write($"Try to apply metamagic: {evt.AppliedMetamagics.StringJoin(m => m.ToString())}, spell {spell.name} allows {spell.AvailableMetamagic}");
            if (evt.AppliedMetamagics.Count == 0) return;
            int reduction = Reduction;
            if (OneMetamagicIsFree)
            {
                // Reduce the cost by the most costly metamagic.
                reduction = evt.AppliedMetamagics.Max(m => m == Metamagic.Heighten ? evt.HeightenLevel : m.DefaultCost());
            }
            Log.Write($"Reduce cost of spell: {spell.name} by {reduction}");
            evt.ReduceCost(reduction);
        }

        public void OnEventDidTrigger(RuleApplyMetamagic evt) { }
    }

    // Doubles the effects of other magic feat bonuses for this spell.
    // Similar to SpellSpecializationParameterized, but for Spell Perfection.
    //
    // This is tricky to implement correctly especially if new feats are added.
    //
    // For now use a hard coded list:
    // - Spell Focus & Greater
    // - Spell Penetration & Greater
    // - Spell Specialization
    // - Elemental Focus
    // - Mage's Tattoo
    //
    // TODO:
    // - Augment Summoning
    // - Superior Summoning
    // - Weapon Focus (ray) & Greater
    // - Weapon Specialization (ray) & Greater
    [AllowedOn(typeof(BlueprintParametrizedFeature))]
    public class SpellPerfectionDoubleFeatBonuses : ParametrizedFeatureComponent, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IInitiatorRulebookHandler<RuleSpellResistanceCheck>, IInitiatorRulebookSubscriber
    {
        public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt)
        {
            var spell = Param.GetValueOrDefault().Blueprint;
            if (evt.Ability != spell && evt.Ability?.Parent != spell) return;

            // Double spell penetration bonus.
            foreach (var fact in Owner.Progression.Features.Enumerable)
            {
                var blueprint = fact.Blueprint;
                if (blueprint == MagicFeats.spellPenetration ||
                    blueprint == MagicFeats.spellPenetrationGreater)
                {
                    foreach (var c in fact.SelectComponents<SpellPenetrationBonus>())
                    {
                        c.OnEventAboutToTrigger(evt);
                    }
                }
            }
        }

        public void OnEventDidTrigger(RuleSpellResistanceCheck evt) { }

        public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            var spell = Param.GetValueOrDefault().Blueprint;
            if (evt.Spell != spell && evt.Spell?.Parent != spell) return;

            // Double other bonuses applied to this spell.
            Log.Append($"Spell Perfection: double other feat bonuses for {spell.name}");
            foreach (var fact in Owner.Progression.Features.Enumerable)
            {
                var blueprint = fact.Blueprint;
                if (blueprint == MagicFeats.spellFocus ||
                    blueprint == MagicFeats.spellFocusGreater)
                {
                    foreach (var c in fact.SelectComponents<SpellFocusParametrized>())
                    {
                        c.OnEventAboutToTrigger(evt);
                    }
                }
                else if (blueprint.name.StartsWith("SpellSpecialization"))
                {
                    // TODO: ideally we'd use AssetId, but there's so many of them for Spell Specialization.
                    // We could probably extract them from the progression, though
                    foreach (var c in fact.SelectComponents<SpellSpecializationParametrized>())
                    {
                        c.OnEventAboutToTrigger(evt);
                    }
                }
                else if (blueprint == MagicFeats.magesTattoo)
                {
                    foreach (var c in fact.SelectComponents<CasterLevelForSchoolParameterized>())
                    {
                        c.OnEventAboutToTrigger(evt);
                    }
                }
                else if (MagicFeats.elementalFocus.AllFeatures.Contains(blueprint) ||
                  MagicFeats.elementalFocusGreater.AllFeatures.Contains(blueprint))
                {
                    foreach (var c in fact.SelectComponents<IncreaseSpellDescriptorDC>())
                    {
                        c.OnEventAboutToTrigger(evt);
                    }
                }
            }
            Log.Flush();
        }

        public void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }
    }

    // Similar to SpellFocusParameterized
    [AllowedOn(typeof(BlueprintParametrizedFeature))]
    public class CasterLevelForSchoolParameterized : ParametrizedFeatureComponent, IInitiatorRulebookHandler<RuleCalculateAbilityParams>
    {
        public int Bonus = 1;

        public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            var spell = evt.Spell;
            if (spell != null && spell.School == Param.GetValueOrDefault().SpellSchool)
            {
                evt.AddBonusCasterLevel(Bonus);
            }
        }

        public void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }
    }

    // Inverse of AddOppositionSchool
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class RemoveOppositionSchool : OwnedGameLogicComponent<UnitDescriptor>
    {
        public BlueprintCharacterClass CharacterClass;

        public SpellSchool School;

        public RemoveOppositionSchool() { }

        public static RemoveOppositionSchool Create(BlueprintCharacterClass @class, SpellSchool school)
        {
            var result = Helpers.Create<RemoveOppositionSchool>();
            result.CharacterClass = @class;
            result.School = school;
            return result;
        }

        public override void OnFactActivate()
        {
            Owner.DemandSpellbook(CharacterClass).OppositionSchools.Remove(School);
        }
    }

    // This is similar to DamageGrace (used by SlashingGrace/FencingGrace) with these changes:
    // - it's not parameterized, so we can hard code the weapon.
    // - it doesn't allow a buckler, per rules as written.
    [AllowMultipleComponents]
    [ComponentName("Replace damage stat for weapon")]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class DamageGraceForWeapon : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>
    {
        public WeaponCategory Category;

        public override void OnTurnOn()
        {
            // Using DamageGracePart should ensure this works correctly with other
            // features that work with finessable wepaons.
            // (e.g. this is how Weapon Finesse picks it up.) 
            Owner.Ensure<DamageGracePart>().AddEntry(Category, Fact);
        }

        public override void OnTurnOff()
        {
            Owner.Ensure<DamageGracePart>().RemoveEntry(Fact);
        }

        public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            if (evt.Weapon.Blueprint.Type.Category == Category)
            {
                var offHand = evt.Initiator.Body.SecondaryHand;
                if (!offHand.HasShield && (!offHand.HasWeapon || offHand.MaybeWeapon == evt.Initiator.Body.EmptyHandWeapon))
                {
                    var dexterity = evt.Initiator.Descriptor.Stats.Dexterity;
                    var existingStat = !evt.DamageBonusStat.HasValue ? null : (Owner.Unit.Descriptor.Stats.GetStat(evt.DamageBonusStat.Value) as ModifiableValueAttributeStat);
                    if (dexterity != null && (existingStat == null || dexterity.Bonus > existingStat.Bonus))
                    {
                        evt.OverrideDamageBonusStat(StatType.Dexterity);
                    }
                }
            }
        }

        public override void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }
    }

    public abstract class CustomParamSelection : BlueprintParametrizedFeature, IFeatureSelection
    {
        FeatureUIData[] cached;

        public new IEnumerable<IFeatureSelectionItem> Items
        {
            get
            {
                try
                {
                    return cached ?? (cached = BlueprintsToItems(GetAllItems()).ToArray());
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return cached = Array.Empty<FeatureUIData>();
                }
            }
        }
        IEnumerable<IFeatureSelectionItem> IFeatureSelection.Items => Items;

        public CustomParamSelection()
        {
            this.ParameterType = FeatureParameterType.Custom;
        }

        protected abstract IEnumerable<BlueprintScriptableObject> GetAllItems();

        protected abstract IEnumerable<BlueprintScriptableObject> GetItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit);

        protected virtual bool CanSelect(UnitDescriptor unit, FeatureParam param) => true;

        bool IFeatureSelection.CanSelect(UnitDescriptor unit, LevelUpState state, FeatureSelectionState selectionState, IFeatureSelectionItem item)
        {
            if (!item.Param.HasValue) return false;
            if (HasNoSuchFeature)
            {
                var feat = item.Param.Value.Blueprint as BlueprintFeature;
                if (feat != null && unit.HasFact(feat)) return false;
            }
            return CanSelect(unit, item.Param.Value);
        }

        IEnumerable<IFeatureSelectionItem> IFeatureSelection.ExtractSelectionItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            try
            {
                Log.Write("IFeatureSelection.ExtractSelectionItems");
                var items = GetItems(beforeLevelUpUnit, previewUnit);
                Log.Append($"GetItems(): {GetType().Name}, name: {name}, guid: {AssetGuid}");
                foreach (var b in items)
                {
                    Log.Append($"  - {b.name}");
                }
                Log.Flush();
                return BlueprintsToItems(items);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return Array.Empty<IFeatureSelectionItem>();
        }

        IEnumerable<FeatureUIData> BlueprintsToItems(IEnumerable<BlueprintScriptableObject> items)
        {
            return items.Select(scriptable =>
            {
                var fact = scriptable as BlueprintUnitFact;
                return new FeatureUIData(this, scriptable, fact?.Name, fact?.Description, fact?.Icon, scriptable.name);
            });
        }
    }

    public class KnownSpellSelection : CustomParamSelection
    {
        protected override IEnumerable<BlueprintScriptableObject> GetItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            if (SpellcasterClass != null)
            {
                var spellbook = previewUnit.GetSpellbook(SpellcasterClass);
                return spellbook != null ? GetKnownSpells(spellbook) : Array.Empty<BlueprintAbility>();
            }
            else
            {
                return previewUnit.Spellbooks.SelectMany(GetKnownSpells);
            }
        }

        protected override IEnumerable<BlueprintScriptableObject> GetAllItems() => Helpers.allSpells;

        IEnumerable<BlueprintAbility> GetKnownSpells(Spellbook spellbook)
        {
            var spells = new List<BlueprintAbility>();
            for (int level = 1; level <= spellbook.MaxSpellLevel; level++)
            {
                foreach (var knownSpell in spellbook.GetKnownSpells(level))
                {
                    spells.Add(knownSpell.Blueprint);
                }
            }
            return spells;
        }
    }

    // Fix Spell Specialization so it can select any spell, instead of being restricted to
    // the class spell list.
    [Harmony12.HarmonyPatch(typeof(BlueprintParametrizedFeature), "ExtractItemsFromSpellbooks", typeof(UnitDescriptor))]
    static class BlueprintParametrizedFeature_ExtractItemsFromSpellbooks_Patch
    {
        static void Postfix(BlueprintParametrizedFeature __instance, UnitDescriptor unit, ref IEnumerable<FeatureUIData> __result)
        {
            try
            {
                var self = __instance;
                List<FeatureUIData> result = null;
                // Add spells that have been granted by a progression (e.g. bloodline, oracle mystery/curse, etc).
                foreach (var feature in unit.Progression.Features)
                {
                    var progression = feature.Blueprint as BlueprintProgression;
                    if (progression == null) continue;
                    var data = unit.Progression.SureProgressionData(progression);
                    foreach (var entry in data.LevelEntries.Where(e => e.Level <= data.Level))
                    {
                        foreach (var addSpell in entry.Features.SelectMany(f => f.GetComponents<AddKnownSpell>()))
                        {
                            var spell = addSpell.Spell;
                            if (unit.GetFeature(self.Prerequisite, spell.School) == null) continue; // skip spells of wrong school

                            if (result == null) result = __result.ToList();
                            result.Add(new FeatureUIData(self, spell, spell.Name, spell.Description, spell.Icon, spell.name));
                        }
                    }
                }
                if (result != null) __result = result;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class MetamagicOnNextSpellFixed : MetamagicOnNextSpell, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IInitiatorRulebookHandler<RuleCastSpell>
    {
        [JsonProperty]
        AbilityData spell;

        void IRulebookHandler<RuleCalculateAbilityParams>.OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            if (DoNotRemove)
            {
                base.OnEventAboutToTrigger(evt);
                return;
            }

            // Base game bug: we can't remove the buff during ability calculation, as it can be
            // triggered multiple times (e.g. casting defensively, concentration, spellstrike, etc).
            // Instead, we'll remove when the spell is cast.

            if (spell != null && evt.AbilityData != spell) return; // already applied
            DoNotRemove = true;
            base.OnEventAboutToTrigger(evt);
            DoNotRemove = false;
            spell = evt.AbilityData; // remember the spell, so we don't apply to a different one.
        }

        public void OnEventAboutToTrigger(RuleCastSpell evt) { }

        public void OnEventDidTrigger(RuleCastSpell evt)
        {
            if (DoNotRemove || spell == null || evt.Spell != spell) return;

            // Our spell was cast, time to remove the buff.
            Buff.Remove();
        }
    }

    // Handles the logic for Fey Foundling's Cold Iron vulnerability and heal bonus.
    // (The saving throw bonus is handled directly in the feat, using an existing component.)
    public class FeyFoundlingLogic : OwnedGameLogicComponent<UnitDescriptor>, ITargetRulebookHandler<RuleHealDamage>, ITargetRulebookHandler<RuleCalculateDamage>
    {
        bool inRuleHealDamage = false;

        public void OnEventAboutToTrigger(RuleHealDamage evt)
        {
            if (inRuleHealDamage) return;
            inRuleHealDamage = true; // prevent reentrancy

            var context = Helpers.GetMechanicsContext();
            int rolls = evt.HealFormula.Rolls;
            if (context != null && currentHealDice != null)
            {
                // For some reason, dice are not passed in to the RuleHealDamage,
                // so we need to capure them in places like ContextActionHealTarget,
                // and then compute the number of rolls here.
                var healDice = currentHealDice;
                var sharedValueCount = Enum.GetValues(typeof(AbilitySharedValue)).Length;
                for (int i = 0; i <= sharedValueCount; i++) // guard against a loop.
                {
                    rolls += healDice.DiceCountValue.Calculate(context);
                    Log.Write($"{GetType().Name}: rolls {rolls} ({healDice.DiceCountValue})");
                    // See if the shared value came from a dice roll
                    if (!healDice.BonusValue.IsValueShared) break;
                    var calcShared = context.AssociatedBlueprint.GetComponents<ContextCalculateSharedValue>()
                        .FirstOrDefault(c => c.ValueType == healDice.BonusValue.ValueShared);
                    if (calcShared == null) break;
                    healDice = calcShared.Value;
                }
            }

            var bonus = rolls * 2; // +2 per dice
            Log.Append($"{GetType().Name}: heal bonus {bonus}, dice {evt.HealFormula}, total bonus {evt.Bonus + bonus}");
            Log.Write($"  context: {context}, currentHealDice: {currentHealDice}");
            Rulebook.CurrentContext.Trigger(new RuleHealDamage(evt.Initiator, evt.Target, evt.HealFormula, evt.Bonus + bonus));

            // Disable the original heal roll, so we don't get double healing
            evt.Modifier = 0;
            inRuleHealDamage = false;
        }

        public void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            var weaponDamage = evt.DamageBundle.WeaponDamage as PhysicalDamage;
            if (weaponDamage != null && (weaponDamage.MaterialsMask & PhysicalDamageMaterial.ColdIron) != 0)
            {
                weaponDamage.AddBonusTargetRelated(1);
            }
        }

        public void OnEventDidTrigger(RuleHealDamage evt) { }

        public void OnEventDidTrigger(RuleCalculateDamage evt) { }

        static ContextDiceValue currentHealDice;

        static FeyFoundlingLogic()
        {
            Main.ApplyPatch(typeof(ContextActionHealTarget_RunAction_Patch), "Fey Foundling (bonus heal)");
            Main.ApplyPatch(typeof(ContextActionBreathOfLife_RunAction_Patch), "Fey Foundling (bonus heal)");
        }

        [Harmony12.HarmonyPatch(typeof(ContextActionHealTarget), "RunAction", new Type[0])]
        static class ContextActionHealTarget_RunAction_Patch
        {
            static bool Prefix(ContextActionHealTarget __instance)
            {
                currentHealDice = __instance.Value;
                return true;
            }
            static void Postfix()
            {
                currentHealDice = null;
            }
        }

        [Harmony12.HarmonyPatch(typeof(ContextActionBreathOfLife), "RunAction", new Type[0])]
        static class ContextActionBreathOfLife_RunAction_Patch
        {
            static bool Prefix(ContextActionBreathOfLife __instance)
            {
                currentHealDice = __instance.Value;
                return true;
            }
            static void Postfix()
            {
                currentHealDice = null;
            }
        }
    }


    public class PrerequisiteCharacterLevelExact : CustomPrerequisite
    {
        public int Level;

        internal static PrerequisiteCharacterLevelExact Create(int level)
        {
            var p = Helpers.Create<PrerequisiteCharacterLevelExact>();
            p.Level = level;
            return p;
        }

        public override bool Check(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state)
        {
            return unit.Progression.CharacterLevel == Level;
        }

        public override string GetCaption() => $"{UIStrings.Instance.Tooltips.CharacterLevel} equals: {Level}";
    }
}
