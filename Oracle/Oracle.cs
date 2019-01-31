// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;
using Newtonsoft.Json;

namespace EldritchArcana
{
    static class OracleClass
    {
        static LibraryScriptableObject library => Main.library;

        // Convenience for accessing the class, and an array only containing the class.
        // Useful for prerequisties, progressions, ContextRankConfig, BlueprintAbilityResource, etc.
        internal static BlueprintCharacterClass oracle;
        internal static BlueprintCharacterClass[] oracleArray;

        internal static void Load()
        {
            if (OracleClass.oracle != null) return;

            var sorcerer = Helpers.GetClass("b3a505fb61437dc4097f43c3f8f9a4cf");
            var cleric = Helpers.GetClass("67819271767a9dd4fbfd4ae700befea0");

            var oracle = OracleClass.oracle = Helpers.Create<BlueprintCharacterClass>();
            oracleArray = new BlueprintCharacterClass[] { oracle };
            oracle.name = "OracleClass";
            library.AddAsset(oracle, "ec73f4790c1d4554871b81cde0b9399b");
            oracle.LocalizedName = Helpers.CreateString("Oracle.Name", "先知");
            oracle.LocalizedDescription = Helpers.CreateString("Oracle.Description", "虽然众神有无数代言人为祂们在地上行走，这些人中最为神秘的一群恐怕莫过于先知了。这些获选者在并不知情的状况下成为了神祇伟力的容器，得以操纵他们并不能完全理解的大能。和通过虔诚侍奉以从他们的主宰手中获得神力的牧师不同，先知的力量可能来源于多种多样的途径，换句话说，他们可以从所有与自己分享同一理念的神祇那里获得支持。先知并不依靠敬拜单一神明，而是向所有与自己的信仰融会贯通的神祇表达敬意。当然，有人将先知掌握的力量视为天赋，而另一些人则将之看作是这些人背负的诅咒——它们以种种不可预料的方法影响着这些获选者的命运之路。\n" +
                "角色定位：先知的任务并不一定与某个特定的教会或神殿相关，他们更喜欢独来独往或是和一小群志同道合的先知合作。运用自己的法术和通过启迪自己所身负的秘示之能，一个先知既可以呼啸沙场战无不胜，也可以医疗疾患救死扶伤。");
            oracle.m_Icon = cleric.Icon;
            oracle.SkillPoints = 4;
            oracle.HitDie = DiceType.D8;
            oracle.BaseAttackBonus = cleric.BaseAttackBonus;
            oracle.FortitudeSave = sorcerer.FortitudeSave;
            oracle.ReflexSave = sorcerer.ReflexSave;
            oracle.WillSave = sorcerer.WillSave;

            // TODO: Oracle will not work properly with Mystic Theurge.
            // Not sure it's worth fixing, but if desired the fix would be:
            // - patch spellbook selection feats, similar to what Crossblooded does.
            // - use a similar apporach as Theurge does for Inquisitor, to select new spells via the feat UI.
            var spellbook = Helpers.Create<BlueprintSpellbook>();
            spellbook.name = "OracleSpellbook";
            library.AddAsset(spellbook, "c26cdf7ee670428c96aad20225f3fdca");
            spellbook.Name = oracle.LocalizedName;
            spellbook.SpellsPerDay = sorcerer.Spellbook.SpellsPerDay;
            spellbook.SpellsKnown = sorcerer.Spellbook.SpellsKnown;
            spellbook.SpellList = cleric.Spellbook.SpellList;
            spellbook.Spontaneous = true;
            spellbook.IsArcane = false;
            spellbook.AllSpellsKnown = false;
            spellbook.CanCopyScrolls = false;
            spellbook.CastingAttribute = StatType.Charisma;
            spellbook.CharacterClass = oracle;
            spellbook.CantripsType = CantripsType.Orisions;
            oracle.Spellbook = spellbook;

            // Consolidated skills make this a bit of a judgement call. Explanation below.
            // Note that Mysteries add 2 more skills typically.
            oracle.ClassSkills = new StatType[] {
                // Oracles have Diplomacy and Sense Motive. Diplomacy is the main component of
                // Persuasion in PF:K. (Also: while Sense Motives should map to Perception with
                // consolidated skills, in PF:K it seems to be more in line with Persuasion).
                StatType.SkillPersuasion,
                // Oracles have Knowledge (history), which is a main component of (world).
                StatType.SkillKnowledgeWorld,
                // Oracles have Knowledge (planes) and Knowledge (religion) so this is an easy call,
                // because those skills are 100% of consolidated Religion skill.
                StatType.SkillLoreReligion,
            };

            oracle.IsDivineCaster = true;
            oracle.IsArcaneCaster = false;

            oracle.StartingGold = cleric.StartingGold; // all classes start with 411.
            oracle.PrimaryColor = cleric.PrimaryColor;
            oracle.SecondaryColor = cleric.SecondaryColor;

            oracle.RecommendedAttributes = new StatType[] { StatType.Charisma };
            oracle.NotRecommendedAttributes = new StatType[] { StatType.Intelligence };

            oracle.EquipmentEntities = cleric.EquipmentEntities;
            oracle.MaleEquipmentEntities = cleric.MaleEquipmentEntities;
            oracle.FemaleEquipmentEntities = cleric.FemaleEquipmentEntities;

            // Both of the restrictions here are relevant (no atheism feature, no animal class).
            oracle.ComponentsArray = cleric.ComponentsArray;
            oracle.StartingItems = cleric.StartingItems;

            var progression = Helpers.CreateProgression("OracleProgression",
                oracle.Name,
                oracle.Description,
                "317a0f107135425faa7def96cb8ef690",
                oracle.Icon,
                FeatureGroup.None);
            progression.Classes = oracleArray;
            var entries = new List<LevelEntry>();

            var orisons = library.CopyAndAdd<BlueprintFeature>(
                "e62f392949c24eb4b8fb2bc9db4345e3", // cleric orisions
                "OracleOrisonsFeature",
                "926891a8e8a74d9eac63a1e296b1a4f3");
            orisons.SetDescription("先知可以了解一些祷念，或称0环法术。祷念的数量见上表。这些法术的施法方式和正常法术相同，不过它们不会消耗任何法术位且可以重复施放。");
            orisons.SetComponents(orisons.ComponentsArray.Select(c =>
            {
                var bind = c as BindAbilitiesToClass;
                if (bind == null) return c;
                bind = UnityEngine.Object.Instantiate(bind);
                bind.CharacterClass = oracle;
                bind.Stat = StatType.Charisma;
                return bind;
            }));
            var proficiencies = library.CopyAndAdd<BlueprintFeature>(
                "8c971173613282844888dc20d572cfc9", // cleric proficiencies
                "OracleProficiencies",
                "baee2212dee249cb8136bda72a872ba4");
            proficiencies.SetName("先知擅长");
            proficiencies.SetDescription("先知擅长所有的简单武器，轻甲，中甲以及所有盾牌（除了塔盾）。一些秘示域会赋予先知额外的武器和护甲擅长。");

            // Note: curses need to be created first, because some revelations use them (e.g. Cinder Dance).
            var curse = OracleCurses.CreateSelection();
            (var mystery, var revelation, var mysteryClassSkills) = CreateMysteryAndRevelationSelection();

            var cureOrInflictSpell = CreateCureOrInflictSpellSelection();

            var detectMagic = library.Get<BlueprintFeature>("ee0b69e90bac14446a4cf9a050f87f2e");
            entries.Add(Helpers.LevelEntry(1,
                proficiencies,
                mystery,
                curse,
                cureOrInflictSpell,
                revelation,
                orisons,
                mysteryClassSkills,
                library.Get<BlueprintFeature>("d3e6275cfa6e7a04b9213b7b292a011c"), // ray calculate feature
                library.Get<BlueprintFeature>("62ef1cdb90f1d654d996556669caf7fa"), // touch calculate feature
                library.Get<BlueprintFeature>("9fc9813f569e2e5448ddc435abf774b3"), // full caster
                detectMagic
            ));
            entries.Add(Helpers.LevelEntry(3, revelation));
            entries.Add(Helpers.LevelEntry(7, revelation));
            entries.Add(Helpers.LevelEntry(11, revelation));
            entries.Add(Helpers.LevelEntry(15, revelation));
            entries.Add(Helpers.LevelEntry(19, revelation));
            progression.UIDeterminatorsGroup = new BlueprintFeatureBase[] {
                mystery, curse, cureOrInflictSpell, proficiencies, orisons, mysteryClassSkills, detectMagic,
            };
            progression.UIGroups = Helpers.CreateUIGroups(
                revelation, revelation, revelation, revelation, revelation, revelation);
            progression.LevelEntries = entries.ToArray();

            oracle.Progression = progression;

            oracle.Archetypes = OracleArchetypes.Create(mystery, revelation, mysteryClassSkills).ToArray();

            oracle.RegisterClass();

            var extraRevelation = Helpers.CreateFeatureSelection("ExtraRevelation",
                "额外启示", "你获得了一个额外启示，你必须满足此启示的所有先决条件。.\n特殊：你可以多次选择此专长。",
                "e91bd89bb5534ae2b61a3222a9b7325e",
                Helpers.GetIcon("fd30c69417b434d47b6b03b9c1f568ff"), // selective channel
                FeatureGroup.Feat,
                Helpers.PrerequisiteClassLevel(oracle, 1));
            var extras = revelation.Features.Select(
                // The level-up UI sometimes loses track of two selections at the same level
                // (e.g. taking Extra Revelations at 1st level),  so clone the feature selections.
                f => library.CopyAndAdd(f, $"{f.name}Extra", f.AssetGuid, "afc8ceb5eb2849d5976e07f5f02ab200")).ToList();
            extras.Add(UndoSelection.Feature.Value);
            extraRevelation.SetFeatures(extras);
            var abundantRevelations = Helpers.CreateFeatureSelection("AbundantRevelations",
                "额外启示次数",
                "选择一项你已拥有的有每日使用次数的启示，此启示获得每日一次额外使用次数。\n特殊：你可以多次选择此专长。它的效果不会叠加。你每次选择此专长，它都只适用于新的启示。",
                "1614c7b40565481fa3728fd7375ddca0",
                Helpers.GetIcon("a2b2f20dfb4d3ed40b9198e22be82030"), // extra lay on hands
                FeatureGroup.Feat);
            var resourceChoices = new List<BlueprintFeature>();
            var prereqRevelations = new List<Prerequisite> { Helpers.PrerequisiteClassLevel(oracle, 1) };
            CreateAbundantRevelations(revelation, abundantRevelations, resourceChoices, prereqRevelations, new HashSet<BlueprintFeature>());
            abundantRevelations.SetFeatures(resourceChoices);
            abundantRevelations.SetComponents(prereqRevelations);

            library.AddFeats(extraRevelation, abundantRevelations);
        }

        static void CreateAbundantRevelations(BlueprintFeature revelation, BlueprintFeatureSelection abundantRevelations, List<BlueprintFeature> resourceChoices, List<Prerequisite> prereqRevelations, HashSet<BlueprintFeature> seen)
        {
            if (revelation == LifeMystery.lifeLink) return;

            bool first = true;
            foreach (var resourceLogic in revelation.GetComponents<AddAbilityResources>())
            {
                if (!seen.Add(revelation)) continue;
                var resource = resourceLogic.Resource;
                var feature = Helpers.CreateFeature($"{abundantRevelations.name}{revelation.name}",
                    $"{abundantRevelations.Name} — {revelation.Name}",
                    $"{abundantRevelations.Description}\n{revelation.Description}",
                    Helpers.MergeIds("d2f3b9be00b04940805bff7b7f60381f", revelation.AssetGuid, resource.AssetGuid),
                    revelation.Icon,
                    FeatureGroup.None,
                    revelation.PrerequisiteFeature(),
                    resource.CreateIncreaseResourceAmount(1));
                resourceChoices.Add(feature);
                if (first)
                {
                    prereqRevelations.Add(revelation.PrerequisiteFeature(true));
                    first = false;
                }
            }
            var selection = revelation as BlueprintFeatureSelection;
            if (selection == null) return;

            foreach (var r in selection.Features)
            {
                CreateAbundantRevelations(r, abundantRevelations, resourceChoices, prereqRevelations, seen);
            }
        }

        static (BlueprintFeatureSelection, BlueprintFeatureSelection, BlueprintFeature) CreateMysteryAndRevelationSelection()
        {
            // This feature allows archetypes to replace mystery class skills with something else.
            var classSkill = Helpers.CreateFeature("MysteryClassSkills", "奖励本职技能",
                "先知基于他们选择的秘视域获得额外的本职技能。",
                "3949c44664d047c99d870b1f3728457c",
                null,
                FeatureGroup.None);

            // TODO: need some additional mysteries.
            //
            // Implemented:
            // - Dragon
            // - Battle
            // - Flame
            // - Life
            // - Time (with Ancient Lorekeeper archetype)
            //
            // Other interesting ones:
            // - Bone (necromancy seems fun; need to learn how summons work though)
            // - Heavens (flashy rainbow spells! Cha to all saves at lvl 20. Some revelations won't work in CRPG.)
            // - Nature (druid-ish, has a restricted animal companion. Some revelations wouldn't work well in CRPG.)
            // - Ancestor (to go with Ancient Lorekeeper)
            //
            // Ancestor would lose 2 revelations because they wouldn't do anything without
            // a GM: Voice of the Grave, Wisdom of the Ancestor. Maybe they can be redesigned
            // to offer a temporary "insight" bonus to certain stats, to represent the
            // information you learned? Or maybe one of these can be reworked into
            // Heroism/Greater Heroism, since that's the 2 spells Loremaster gives up,
            // and duplicating spells as SLAs is common for revelations.)

            var mysteryDescription = "每一个先知都拥有属于自己的神力奥秘以为力量和法术之源，这些秘示域也同样会给予他们以额外的本职技能和其他特殊能力。秘示域代表了一个先知所追随的信念以及对于支持类型概念神祇的赞美，或是这些大能赋予一个代行者发自内心的召唤。举例来说，一个拥有波涛秘示域的先知可能是因为她在在风口浪尖之间出生长大、并受到了江河湖海之神的启迪，作为水之平静与狂暴双面的化身在人间行走。这些来源不明的能力将随着先知等级的提升而渐变演化为更多更强的力量，在她的第一个等级先知需要选择一个秘示域，一旦选定即无法更改。\n" +
                            "在2级，以及之后每提升2个等级时，取决于她的秘示域，先知可以将额外的一个法术加入她的已知法术列表。这些法术计在她已知法术数量之外，在提升等级时，先知无法替换这些法术。";

            var mysteriesAndRevelations = new (BlueprintFeature, BlueprintFeature)[] {
                BattleMystery.Create(mysteryDescription, classSkill),
                DragonMystery.Create(mysteryDescription, classSkill),
                FlameMystery.Create(mysteryDescription, classSkill),
                LifeMystery.Create(mysteryDescription, classSkill),
                TimeMystery.Create(mysteryDescription, classSkill),
            };
            var mysteryChoice = Helpers.CreateFeatureSelection("OracleMysterySelection", "秘视域",
                            mysteryDescription,
                            "ec3a4ede658f4b2696c89bdd590b5e04",
                            null,
                            UpdateLevelUpDeterminatorText.Group);
            mysteryChoice.SetFeatures(mysteriesAndRevelations.Select(m => m.Item1));
            var revelationChoice = Helpers.CreateFeatureSelection("OracleRevelationSelection", "启示",
                "在1级，3级以及之后每提升4个等级时（7级，11级，以此类推），先知可以从自己的秘示域中学习和领悟到新的力量与能力。先知只能选择她秘示域所给予的启示，如果一项启示具有随等级提升的效能，先知以她的现有等级来计算这些效果。除非特别提及，否则激活启示能力将是一个标准动作。\n" +
                "除非另有说明，否则对抗先知启示的DC为10+先知等级的一半+先知的魅力调整值。",
                "1dd88ec42dc249ca94bf3c2fc239064d",
                null,
                FeatureGroup.None);
            revelationChoice.SetFeatures(mysteriesAndRevelations.Select(m => m.Item2));

            return (mysteryChoice, revelationChoice, classSkill);
        }

        static BlueprintFeatureSelection CreateCureOrInflictSpellSelection()
        {
            var selection = Helpers.CreateFeatureSelection("OracleCureOrInflictSpellSelection", "治愈或伤害法术",
                "除了先知在升级时获得的法术，每个先知还可以将治愈X伤或造成X伤系列法术加入其已知法术列表。 (治愈法术包括所有名字中含有 “治愈X伤” 的法术, 伤害法术包括所有名字中含有 “造成X伤” 的法术)。一旦先知达到了可以释放这些法术的等级，他就会立刻习得它们。此选择必须在先知等级一时做出，并且无法更改。",
                "4e685b25900246939394662b7fa36295",
                null,
                UpdateLevelUpDeterminatorText.Group);

            var cureProgression = Helpers.CreateProgression("OracleCureSpellProgression",
                "治愈法术",
                selection.Description,
                "99b17564aaf94886b6858c92eec20285",
                Helpers.GetIcon("47808d23c67033d4bbab86a1070fd62f"), // cure light wounds
                FeatureGroup.None);
            cureProgression.Classes = oracleArray;

            var cureSpells = Bloodlines.CreateSpellProgression(cureProgression, cureSpellIds);
            var cureEntries = new List<LevelEntry>();
            for (int level = 1; level <= 8; level++)
            {
                int classLevel = level == 1 ? 1 : level * 2;
                cureEntries.Add(Helpers.LevelEntry(classLevel, cureSpells[level - 1]));
            }
            cureProgression.LevelEntries = cureEntries.ToArray();
            cureProgression.UIGroups = Helpers.CreateUIGroups(cureSpells);

            var inflictProgression = Helpers.CreateProgression("OracleInflictSpellProgression",
                "伤害法术",
                selection.Description,
                "1ad92576cf214c9a8890cd9ef6a06a31",
                Helpers.GetIcon("e5cb4c4459e437e49a4cd73fde6b9063"), // inflict light wounds
                FeatureGroup.None);
            inflictProgression.Classes = oracleArray;

            var inflictSpells = Bloodlines.CreateSpellProgression(inflictProgression, new String[] {
                "e5af3674bb241f14b9a9f6b0c7dc3d27", // inflict light wounds
                "65f0b63c45ea82a4f8b8325768a3832d", // moderate
                "bd5da98859cf2b3418f6d68ea66cabbe", // serious
                "651110ed4f117a948b41c05c5c7624c0", // critical
                "9da37873d79ef0a468f969e4e5116ad2", // light, mass
                "03944622fbe04824684ec29ff2cec6a7", // moderate, mass
                "820170444d4d2a14abc480fcbdb49535", // serious, mass
                "5ee395a2423808c4baf342a4f8395b19", // critical, mass
            });
            var inflictEntries = new List<LevelEntry>();
            for (int level = 1; level <= 8; level++)
            {
                int classLevel = level == 1 ? 1 : level * 2;
                inflictEntries.Add(Helpers.LevelEntry(classLevel, inflictSpells[level - 1]));
            }
            inflictProgression.LevelEntries = inflictEntries.ToArray();
            inflictProgression.UIGroups = Helpers.CreateUIGroups(inflictSpells);

            selection.SetFeatures(cureProgression, inflictProgression);
            return selection;
        }

        internal static BindAbilitiesToClass CreateBindToOracle(params BlueprintAbility[] abilities)
        {
            return Helpers.Create<BindAbilitiesToClass>(b =>
            {
                b.Stat = StatType.Charisma;
                b.Abilites = abilities;
                b.CharacterClass = oracle;
                b.AdditionalClasses = Array.Empty<BlueprintCharacterClass>();
                b.Archetypes = Array.Empty<BlueprintArchetype>();
            });
        }

        internal static Lazy<BlueprintAbility[]> cureSpells = new Lazy<BlueprintAbility[]>(() =>
            OracleClass.cureSpellIds.Select(library.Get<BlueprintAbility>).ToArray());

        static String[] cureSpellIds = new String[] {
            "5590652e1c2225c4ca30c4a699ab3649", // cure light wounds
            "6b90c773a6543dc49b2505858ce33db5", // moderate
            "3361c5df793b4c8448756146a88026ad", // serious
            "41c9016596fe1de4faf67425ed691203", // critical
            "5d3d689392e4ff740a761ef346815074", // light, mass
            "571221cc141bc21449ae96b3944652aa", // moderate, mass
            "0cea35de4d553cc439ae80b3a8724397", // serious, mass
            "1f173a16120359e41a20fc75bb53d449", // critical, mass
        };
    }

    // Adds a feature based on a level range and conditional.
    //
    // This makes it easier to implement complex conditions (e.g. Oracle Dragon Mystery Resistances)
    // without needing to create nested BlueprintFeatures and/or BlueprintProgressions.
    //
    // Essentially this combines AddFeatureIfHasFact with two AddFeatureOnClassLevels, to express:
    //
    //     if (MinLevel <= ClassLevel && ClassLevel <= MaxLevelInclusive &&
    //         (CheckedFact == null || HasFeature(CheckedFact) != Not)) {
    //         AddFact(Feature);
    //     }
    //
    [AllowMultipleComponents]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddFactOnLevelRange : AddFactOnLevelUpCondtion
    {
        // The class to use for `MinLevel` and `MaxLevelInclusive`.
        // Optionally `AdditionalClasses` and `Archetypes` can be specified for more classes/archetypes.
        public BlueprintCharacterClass Class;

        // Optionally specifies the feature to check for.        
        public BlueprintUnitFact CheckedFact;

        // If `CheckedFact` is supplied, this indicates whether we want it to be present or
        // not present.
        public bool Not;

        public BlueprintCharacterClass[] AdditionalClasses = Array.Empty<BlueprintCharacterClass>();

        public BlueprintArchetype[] Archetypes = Array.Empty<BlueprintArchetype>();

        protected override int CalcLevel() => ReplaceCasterLevelOfAbility.CalculateClassLevel(Class, AdditionalClasses, Owner, Archetypes);

        protected override bool IsFeatureShouldBeApplied(int level)
        {
            return base.IsFeatureShouldBeApplied(level) && (CheckedFact == null || Owner.HasFact(CheckedFact) != Not);
        }
    }


    // A customizable "add fact on level", that also can interact with the level-up UI
    // in a similar way to progressions and features the player picked.
    // (This is similar to how the level-up UI calls LevelUpHelper.AddFeatures).
    public abstract class AddFactOnLevelUpCondtion : OwnedGameLogicComponent<UnitDescriptor>, IUnitGainLevelHandler
    {
        // The min and max (inclusive) levels in which to apply this feature.
        public int MinLevel = 1, MaxLevelInclusive = 20;

        // The feature to add, if the condition(s) are met.
        public BlueprintUnitFact Feature;

        [JsonProperty]
        private Fact appliedFact;

        public override void OnFactActivate() => Apply();

        public override void OnFactDeactivate()
        {
            Owner.RemoveFact(appliedFact);
            appliedFact = null;
        }

        public override void PostLoad()
        {
            base.PostLoad();
            if (appliedFact != null && !Owner.HasFact(appliedFact))
            {
                appliedFact.Dispose();
                appliedFact = null;
                if (BlueprintRoot.Instance.PlayerUpgradeActions.AllowedForRestoreFeatures.HasItem(Feature))
                {
                    Apply();
                }
            }
        }

        protected abstract int CalcLevel();

        protected virtual bool IsFeatureShouldBeApplied(int level)
        {
            Log.Write($"AddFactOnLevelUpCondtion::IsFeatureShouldBeApplied({level}), MinLevel {MinLevel}, MaxLevelInclusive {MaxLevelInclusive}");
            return level >= MinLevel && level <= MaxLevelInclusive;
        }

        public void HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit == Owner) Apply();
        }

        private Fact Apply()
        {
            Log.Write($"AddFactOnLevelUpCondtion::Apply(), name: {Fact.Blueprint.name}");
            var level = CalcLevel();
            if (IsFeatureShouldBeApplied(level))
            {
                if (appliedFact == null)
                {
                    appliedFact = Owner.AddFact(Feature, null, (Fact as Feature)?.Param);
                    OnAddLevelUpFeature(level);
                }
            }
            else if (appliedFact != null)
            {
                Owner.RemoveFact(appliedFact);
                appliedFact = null;
            }
            return appliedFact;
        }

        private void OnAddLevelUpFeature(int level)
        {
            Log.Write($"AddFactOnLevelUpCondtion::OnAddLevelUpFeature(), name: {Fact.Blueprint.name}");
            var fact = appliedFact;
            if (fact == null) return;

            var feature = fact.Blueprint as BlueprintFeature;
            if (feature == null) return;

            // If we're in the level-up UI, update selections/progressions as needed.
            var unit = Owner;
            var levelUp = Game.Instance.UI.CharacterBuildController.LevelUpController;
            if (unit == levelUp.Preview || unit == levelUp.Unit)
            {
                var selection = feature as BlueprintFeatureSelection;
                if (selection != null)
                {
                    Log.Write($"{GetType().Name}: add selection ${selection.name}");
                    levelUp.State.AddSelection(null, selection, selection, level);
                }
                var progression = feature as BlueprintProgression;
                if (progression != null)
                {
                    Log.Write($"{GetType().Name}: update progression ${selection.name}");
                    LevelUpHelper.UpdateProgression(levelUp.State, unit, progression);
                }
            }
        }
    }



    [AllowMultipleComponents]
    public class AddClassSkillIfHasFeature : OwnedGameLogicComponent<UnitDescriptor>, IUnitGainLevelHandler
    {
        public StatType Skill;
        public BlueprintUnitFact CheckedFact;

        [JsonProperty]
        bool applied;

        internal static AddClassSkillIfHasFeature Create(StatType skill, BlueprintUnitFact feature)
        {
            var a = Helpers.Create<AddClassSkillIfHasFeature>();
            a.name = $"AddClassSkillIfHasFeature${skill}";
            a.Skill = skill;
            a.CheckedFact = feature;
            return a;
        }

        public override void OnTurnOn()
        {
            base.OnTurnOn();
            Apply();
        }

        public override void OnTurnOff()
        {
            base.OnTurnOff();
            Remove();
        }

        void Apply()
        {
            if (Owner.HasFact(CheckedFact))
            {
                if (!applied)
                {
                    var stat = Owner.Stats.GetStat<ModifiableValueSkill>(Skill);
                    stat?.ClassSkill.Retain();
                    stat?.UpdateValue();
                    applied = true;
                }
            }
            else
            {
                Remove();
            }
        }

        void Remove()
        {
            if (applied)
            {
                var stat = Owner.Stats.GetStat<ModifiableValueSkill>(Skill);
                stat?.ClassSkill.Release();
                stat?.UpdateValue();
                applied = false;
            }
        }

        public void HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit == Owner) Apply();
        }
    }
}
