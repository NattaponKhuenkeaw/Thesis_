using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterSkillPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Skill Entries")]
        [SerializeField] private Transform entryContainer;
        [SerializeField] private GameObject entryTemplate;

        private readonly List<SkillEntryView> entryViews = new List<SkillEntryView>();

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null)
            {
                return;
            }

            List<SkillDisplayEntry> entries = BuildSkillEntries(character.characterObjectData);
            RebuildEntryViews(entries);
        }

        private List<SkillDisplayEntry> BuildSkillEntries(CharacterObject data)
        {
            List<SkillDisplayEntry> entries = new List<SkillDisplayEntry>(3);
            if (data == null || data.kitDefinition == null)
            {
                return entries;
            }

            PlayerMetaProgression metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            AddActionEntry(entries, data, PotentialTrack.Skill, metaProgression);
            AddActionEntry(entries, data, PotentialTrack.Ultimate, metaProgression);
            AddPassiveEntry(entries, data, metaProgression);
            return entries;
        }

        private static void AddActionEntry(
            List<SkillDisplayEntry> entries,
            CharacterObject data,
            PotentialTrack track,
            PlayerMetaProgression metaProgression)
        {
            CombatAction action = ResolvePrimaryAction(data, track);
            if (action == null)
            {
                return;
            }

            int level = ResolvePotentialLevel(data, track, metaProgression);
            ActionDisplayValues values = ResolveActionDisplayValues(data, action, track, level);
            ProjectileData projectileData = values.projectileData;
            float hitRatio = ResolveFirstDamageRatio(action, projectileData);
            int hitCount = projectileData != null ? Mathf.Max(1, projectileData.maxHits) : 1;
            float damagePercent = Mathf.Max(0f, values.damageMultiplier * hitRatio * 100f);
            ElementType elementType = ResolveFirstActionElement(action, projectileData);
            CharacterAbilityDefinition ability = ResolveAbility(data != null ? data.kitDefinition : null, track);

            string description = ResolveAuthoredDescriptionOverride(data, track, level)
                ?? BuildGeneratedActionDescription(ability, elementType, damagePercent, hitCount, values.damageSourceStat);

            string metadata = BuildActionMetadata(track, values);
            entries.Add(new SkillDisplayEntry
            {
                icon = ability != null ? ability.icon : null,
                levelLabel = BuildLevelLabel(level),
                title = $"{(track == PotentialTrack.Skill ? "Skill" : "Ultimate")} : {ResolveActionName(action, track)}",
                metadata = metadata,
                description = description
            });
        }

        private static void AddPassiveEntry(
            List<SkillDisplayEntry> entries,
            CharacterObject data,
            PlayerMetaProgression metaProgression)
        {
            CharacterKitDefinition kit = data != null ? data.kitDefinition : null;
            if (kit == null)
            {
                return;
            }

            int level = ResolvePotentialLevel(data, PotentialTrack.Passive, metaProgression);
            PotentialNodeDefinition passiveNode = ResolveBestDisplayNode(data, PotentialTrack.Passive, level)
                ?? kit.basePassiveNode;
            string description = ResolveAuthoredDescriptionOverride(data, PotentialTrack.Passive, level)
                ?? ResolveBasePassiveSkillPanelDescription(kit.basePassiveNode)
                ?? ResolveAbilityDescription(kit.passive)
                ?? "No passive effect";

            entries.Add(new SkillDisplayEntry
            {
                icon = kit.passive != null ? kit.passive.icon : null,
                levelLabel = BuildLevelLabel(level),
                title = $"Passive : {ResolvePassiveName(kit.passive, passiveNode)}",
                metadata = string.Empty,
                description = description
            });
        }

        private void RebuildEntryViews(IReadOnlyList<SkillDisplayEntry> entries)
        {
            if (entryContainer == null || entryTemplate == null)
            {
                return;
            }

            int count = entries != null ? entries.Count : 0;
            EnsureEntryViewCount(count);
            for (int i = 0; i < entryViews.Count; i++)
            {
                bool active = i < count;
                entryViews[i].Root.SetActive(active);
                if (active)
                {
                    entryViews[i].Bind(entries[i]);
                    entryViews[i].RefreshLayout();
                }
            }

            if (entryContainer is RectTransform containerRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            }
        }

        private void EnsureEntryViewCount(int count)
        {
            while (entryTemplate != null && entryViews.Count < count)
            {
                entryViews.Add(CreateEntryView());
            }
        }

        private SkillEntryView CreateEntryView()
        {
            GameObject instance = Instantiate(entryTemplate, entryContainer);
            instance.SetActive(true);
            return SkillEntryView.FromExisting(instance);
        }

        private static string BuildActionMetadata(PotentialTrack track, ActionDisplayValues values)
        {
            List<string> parts = new List<string>(3);
            if (track == PotentialTrack.Skill && values.cooldown > 0f)
            {
                parts.Add($"Cooldown {FormatSeconds(values.cooldown)}");
            }
            else if (track == PotentialTrack.Ultimate)
            {
                if (values.cooldown > 0f)
                {
                    parts.Add($"Cooldown {FormatSeconds(values.cooldown)}");
                }

                if (values.energyCost > 0f)
                {
                    parts.Add($"AE Cost {Mathf.RoundToInt(values.energyCost)}");
                }
            }

            return string.Join(" / ", parts);
        }

        private static CombatAction ResolvePrimaryAction(CharacterObject data, PotentialTrack track)
        {
            ActionResolverTable table = data != null ? data.actionResolverTable : null;
            if (table == null)
            {
                return null;
            }

            IList<CombatAction> actions = track switch
            {
                PotentialTrack.Skill => table.skillActions != null ? new List<CombatAction>(table.skillActions) : null,
                PotentialTrack.Ultimate => table.ultimateActions != null ? new List<CombatAction>(table.ultimateActions) : null,
                _ => null
            };

            return actions != null && actions.Count > 0 ? actions[0] : null;
        }

        private static CharacterAbilityDefinition ResolveAbility(CharacterKitDefinition kit, PotentialTrack track)
        {
            return track switch
            {
                PotentialTrack.Skill => kit != null ? kit.skill : null,
                PotentialTrack.Ultimate => kit != null ? kit.ultimate : null,
                PotentialTrack.Passive => kit != null ? kit.passive : null,
                _ => null
            };
        }

        private static string ResolveActionName(CombatAction action, PotentialTrack track)
        {
            return action != null && !string.IsNullOrWhiteSpace(action.actionName)
                ? action.actionName
                : track.ToString();
        }

        private static string ResolvePassiveName(CharacterAbilityDefinition passive, PotentialNodeDefinition passiveNode)
        {
            if (passiveNode != null && !string.IsNullOrWhiteSpace(passiveNode.nodeName))
            {
                return passiveNode.nodeName;
            }

            return passive != null && !string.IsNullOrWhiteSpace(passive.abilityName)
                ? passive.abilityName
                : "Passive";
        }

        private static ActionDisplayValues ResolveActionDisplayValues(
            CharacterObject data,
            CombatAction action,
            PotentialTrack track,
            int level)
        {
            ActionDisplayValues values = new ActionDisplayValues
            {
                damageMultiplier = action != null ? action.actionDamageMultiplier : 0f,
                damageSourceStat = action != null ? action.damageSourceStat : ActionDamageSourceStat.Attack,
                cooldown = action is SkillAction skillAction ? skillAction.cooldown
                    : action is UltimateAction ultimateAction ? ultimateAction.cooldown : 0f,
                energyCost = action is UltimateAction ultimate ? ultimate.energyCost : 0f,
                projectileData = ResolveFirstProjectileData(action)
            };

            foreach (PotentialNodeDefinition node in EnumerateActiveNodes(data, track, level))
            {
                if (node.actionModifiers == null)
                {
                    continue;
                }

                for (int i = 0; i < node.actionModifiers.Count; i++)
                {
                    PassiveActionModifierDefinition modifier = node.actionModifiers[i];
                    string stepName = ResolveRootStepName(action);
                    if (modifier == null
                        || action == null
                        || !modifier.Matches(action.Category, action.actionId, action.actionName, stepName, null, ignoreRuntimeRequirements: true))
                    {
                        continue;
                    }

                    values.damageMultiplier = ApplyValueModifier(values.damageMultiplier, modifier.damageMultiplierOperation, modifier.damageMultiplierValue);
                    values.cooldown = ApplyValueModifier(values.cooldown, modifier.cooldownOperation, modifier.cooldownValue);
                    values.energyCost = ApplyValueModifier(values.energyCost, modifier.energyCostOperation, modifier.energyCostValue);
                    if (modifier.replacementProjectileData != null)
                    {
                        values.projectileData = modifier.replacementProjectileData;
                    }
                }
            }

            return values;
        }

        private static IEnumerable<PotentialNodeDefinition> EnumerateActiveNodes(CharacterObject data, PotentialTrack track, int level)
        {
            CharacterKitDefinition kit = data != null ? data.kitDefinition : null;
            if (kit == null)
            {
                yield break;
            }

            foreach (PotentialNodeDefinition node in kit.EnumerateAllNodes())
            {
                if (node == null || node.track != track)
                {
                    continue;
                }

                if (node.level <= 0 || node.unlockedByDefault || node.level <= level)
                {
                    yield return node;
                }
            }
        }

        private static int ResolvePotentialLevel(CharacterObject data, PotentialTrack track, PlayerMetaProgression metaProgression)
        {
            int level = metaProgression != null ? metaProgression.GetPotentialLevel(data, track) : 0;
            CharacterKitDefinition kit = data != null ? data.kitDefinition : null;
            if (kit == null)
            {
                return level;
            }

            foreach (PotentialNodeDefinition node in kit.EnumerateAllNodes())
            {
                if (node != null && node.track == track && node.unlockedByDefault)
                {
                    level = Mathf.Max(level, node.level);
                }
            }

            return Mathf.Clamp(level, 0, CharacterKitDefinition.MaxPotentialLevel);
        }

        private static string ResolveAuthoredDescriptionOverride(CharacterObject data, PotentialTrack track, int level)
        {
            PotentialNodeDefinition bestNode = ResolveBestDisplayNode(data, track, level, requireSkillPanelDescription: true);
            return ResolveSkillPanelDescription(bestNode);
        }

        private static PotentialNodeDefinition ResolveBestDisplayNode(
            CharacterObject data,
            PotentialTrack track,
            int level,
            bool requireSkillPanelDescription = false)
        {
            PotentialNodeDefinition bestNode = null;
            foreach (PotentialNodeDefinition node in EnumerateActiveNodes(data, track, level))
            {
                if (node == null)
                {
                    continue;
                }

                if (requireSkillPanelDescription && string.IsNullOrWhiteSpace(node.skillPanelDescriptionOverride))
                {
                    continue;
                }

                if (bestNode == null || node.level >= bestNode.level)
                {
                    bestNode = node;
                }
            }

            return bestNode;
        }

        private static string ResolveSkillPanelDescription(PotentialNodeDefinition node)
        {
            return node != null && !string.IsNullOrWhiteSpace(node.skillPanelDescriptionOverride)
                ? node.skillPanelDescriptionOverride.Trim()
                : null;
        }

        private static string ResolveBasePassiveSkillPanelDescription(PotentialNodeDefinition node)
        {
            return ResolveSkillPanelDescription(node);
        }

        private static string ResolveAbilityDescription(CharacterAbilityDefinition ability)
        {
            return ability != null && !string.IsNullOrWhiteSpace(ability.description)
                ? ability.description.Trim()
                : null;
        }

        private static string BuildGeneratedActionDescription(
            CharacterAbilityDefinition ability,
            ElementType elementType,
            float damagePercent,
            int hitCount,
            ActionDamageSourceStat damageSourceStat)
        {
            StringBuilder description = new StringBuilder();
            string abilityDescription = ResolveAbilityDescription(ability);
            if (!string.IsNullOrWhiteSpace(abilityDescription))
            {
                description.Append(abilityDescription);
                if (damagePercent > 0.001f)
                {
                    description.Append(' ');
                }
            }

            if (damagePercent <= 0.001f)
            {
                return description.ToString().Trim();
            }

            description.Append("Inflict ");
            if (elementType != ElementType.None)
            {
                description.Append(elementType);
                description.Append(" DMG ");
            }

            description.Append(Mathf.RoundToInt(damagePercent));
            description.Append("% of ");
            description.Append(FormatDamageSource(damageSourceStat));
            if (hitCount > 1)
            {
                description.Append(" for ");
                description.Append(hitCount);
                description.Append(" Hit(s)");
            }

            return description.ToString();
        }

        private static string FormatDamageSource(ActionDamageSourceStat damageSourceStat)
        {
            return damageSourceStat switch
            {
                ActionDamageSourceStat.MaxHP => "Max HP",
                ActionDamageSourceStat.Defense => "DEF",
                _ => "ATK"
            };
        }

        private static float ApplyValueModifier(float current, PassiveValueModifierOperation operation, float value)
        {
            return operation switch
            {
                PassiveValueModifierOperation.Add => current + value,
                PassiveValueModifierOperation.Multiply => current * value,
                PassiveValueModifierOperation.Override => value,
                _ => current
            };
        }

        private static ElementType ResolveFirstActionElement(CombatAction action, ProjectileData projectileData = null)
        {
            HitboxBase hitbox = ResolveFirstDamageHitbox(action, projectileData);
            return hitbox switch
            {
                NormalHitbox normalHitbox => normalHitbox.elementType,
                GrabHitbox grabHitbox => grabHitbox.elementType,
                _ => ElementType.None
            };
        }

        private static float ResolveFirstDamageRatio(CombatAction action, ProjectileData projectileData)
        {
            HitboxBase hitbox = ResolveFirstDamageHitbox(action, projectileData);
            return hitbox switch
            {
                NormalHitbox normalHitbox => Mathf.Max(0f, normalHitbox.damageRatio),
                GrabHitbox grabHitbox => Mathf.Max(0f, grabHitbox.damageRatio),
                _ => 0f
            };
        }

        private static HitboxBase ResolveFirstDamageHitbox(CombatAction action, ProjectileData projectileData)
        {
            HitboxBase actionHitbox = ResolveFirstActionDamageHitbox(action);
            if (actionHitbox != null)
            {
                return actionHitbox;
            }

            if (projectileData != null && projectileData.routineHitbox != null && projectileData.routineHitbox.HasHitbox)
            {
                return projectileData.routineHitbox.hitbox;
            }

            return null;
        }

        private static HitboxBase ResolveFirstActionDamageHitbox(CombatAction action)
        {
            ComboNode rootNode = action != null ? action.comboTree?.rootNode : null;
            HashSet<ComboNode> visited = new HashSet<ComboNode>();
            return ResolveFirstActionDamageHitbox(rootNode, visited);
        }

        private static HitboxBase ResolveFirstActionDamageHitbox(ComboNode node, HashSet<ComboNode> visited)
        {
            if (node == null || !visited.Add(node))
            {
                return null;
            }

            HitboxBase stepHitbox = ResolveFirstStepDamageHitbox(node.stepData);
            if (stepHitbox != null)
            {
                return stepHitbox;
            }

            if (node.defaultNext != null)
            {
                HitboxBase defaultHitbox = ResolveFirstActionDamageHitbox(node.defaultNext, visited);
                if (defaultHitbox != null)
                {
                    return defaultHitbox;
                }
            }

            if (node.onGrabSuccessNode != null)
            {
                HitboxBase grabSuccessHitbox = ResolveFirstActionDamageHitbox(node.onGrabSuccessNode, visited);
                if (grabSuccessHitbox != null)
                {
                    return grabSuccessHitbox;
                }
            }

            if (node.routes != null)
            {
                for (int i = 0; i < node.routes.Count; i++)
                {
                    HitboxBase routeHitbox = ResolveFirstActionDamageHitbox(node.routes[i]?.targetNode, visited);
                    if (routeHitbox != null)
                    {
                        return routeHitbox;
                    }
                }
            }

            return null;
        }

        private static HitboxBase ResolveFirstStepDamageHitbox(AttackStepData stepData)
        {
            if (stepData?.hitData?.hitboxes == null)
            {
                return null;
            }

            for (int i = 0; i < stepData.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = stepData.hitData.hitboxes[i];
                if (hitbox is NormalHitbox || hitbox is GrabHitbox)
                {
                    return hitbox;
                }
            }

            return null;
        }

        private static string ResolveRootStepName(CombatAction action)
        {
            return action != null ? action.comboTree?.rootNode?.stepData?.stepName : null;
        }

        private static ProjectileData ResolveFirstProjectileData(CombatAction action)
        {
            ComboNode rootNode = action != null ? action.comboTree?.rootNode : null;
            if (rootNode == null || rootNode.stepData?.hitData?.hitboxes == null)
            {
                return null;
            }

            for (int i = 0; i < rootNode.stepData.hitData.hitboxes.Count; i++)
            {
                if (rootNode.stepData.hitData.hitboxes[i] is ProjectileSpawnHitbox projectileSpawnHitbox
                    && projectileSpawnHitbox.projectileData != null)
                {
                    return projectileSpawnHitbox.projectileData;
                }
            }

            return null;
        }

        private static string FormatSeconds(float seconds)
        {
            return $"{seconds:0.#}s";
        }

        private static string BuildLevelLabel(int level)
        {
            return level <= 0 ? "Lv.0 Inactive" : $"Lv.{level}";
        }

        private struct ActionDisplayValues
        {
            public float damageMultiplier;
            public ActionDamageSourceStat damageSourceStat;
            public float cooldown;
            public float energyCost;
            public ProjectileData projectileData;
        }

        private struct SkillDisplayEntry
        {
            public Sprite icon;
            public string levelLabel;
            public string title;
            public string metadata;
            public string description;
        }

        private class SkillEntryView
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text LevelText;
            public TMP_Text TitleText;
            public TMP_Text MetadataText;
            public TMP_Text DescriptionText;

            public static SkillEntryView FromExisting(GameObject root)
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                Image[] images = root.GetComponentsInChildren<Image>(true);
                TMP_Text levelText = FindTextByName(texts, "Level", "Lv");
                TMP_Text titleText = FindTextByName(texts, "Title", "Header");
                TMP_Text metadataText = FindTextByName(texts, "Metadata", "Meta", "Cost", "Cooldown");
                TMP_Text descriptionText = FindTextByName(texts, "Description", "Desc", "Body");

                titleText ??= texts.Length > 0 ? texts[0] : null;
                metadataText ??= texts.Length > 1 ? texts[1] : null;
                descriptionText ??= texts.Length > 2 ? texts[2] : texts.Length > 0 ? texts[^1] : null;

                return new SkillEntryView
                {
                    Root = root,
                    Icon = images.Length > 1 ? images[1] : images.Length > 0 ? images[0] : null,
                    LevelText = levelText,
                    TitleText = titleText,
                    MetadataText = metadataText,
                    DescriptionText = descriptionText
                };
            }

            public void Bind(SkillDisplayEntry entry)
            {
                if (Icon != null)
                {
                    Icon.sprite = entry.icon;
                    Icon.enabled = entry.icon != null;
                }

                if (LevelText != null)
                {
                    LevelText.text = entry.levelLabel;
                    LevelText.gameObject.SetActive(!string.IsNullOrWhiteSpace(entry.levelLabel));
                }

                if (TitleText != null)
                {
                    TitleText.text = entry.title;
                }

                if (MetadataText != null)
                {
                    string metadata = LevelText == null
                        ? CombineMetadata(entry.levelLabel, entry.metadata)
                        : entry.metadata;
                    MetadataText.text = metadata;
                    MetadataText.gameObject.SetActive(!string.IsNullOrWhiteSpace(metadata));
                }

                if (DescriptionText != null)
                {
                    DescriptionText.richText = true;
                    DescriptionText.text = entry.description;
                }
            }

            public void RefreshLayout()
            {
                if (Root == null)
                {
                    return;
                }

                if (LevelText != null && LevelText.gameObject.activeSelf)
                {
                    LevelText.ForceMeshUpdate();
                }
                if (TitleText != null && TitleText.gameObject.activeSelf)
                {
                    TitleText.ForceMeshUpdate();
                }
                if (MetadataText != null && MetadataText.gameObject.activeSelf)
                {
                    MetadataText.ForceMeshUpdate();
                }
                if (DescriptionText != null && DescriptionText.gameObject.activeSelf)
                {
                    DescriptionText.ForceMeshUpdate();
                }

                VerticalLayoutGroup[] layouts = Root.GetComponentsInChildren<VerticalLayoutGroup>(true);
                for (int i = 0; i < layouts.Length; i++)
                {
                    VerticalLayoutGroup layout = layouts[i];
                    if (layout != null)
                    {
                        RectTransform rectTransform = layout.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            layout.childControlWidth = true;
                            layout.childControlHeight = true;

                            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

                            layout.childControlWidth = false;
                            layout.childControlHeight = false;
                        }
                    }
                }
            }

            private static TMP_Text FindTextByName(TMP_Text[] texts, params string[] tokens)
            {
                if (texts == null || tokens == null)
                {
                    return null;
                }

                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null)
                    {
                        continue;
                    }

                    string name = text.gameObject.name;
                    for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                    {
                        if (!string.IsNullOrWhiteSpace(tokens[tokenIndex])
                            && name.IndexOf(tokens[tokenIndex], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return text;
                        }
                    }
                }

                return null;
            }

            private static string CombineMetadata(string levelLabel, string metadata)
            {
                if (string.IsNullOrWhiteSpace(levelLabel))
                {
                    return metadata;
                }

                return string.IsNullOrWhiteSpace(metadata)
                    ? levelLabel
                    : $"{levelLabel} / {metadata}";
            }
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
