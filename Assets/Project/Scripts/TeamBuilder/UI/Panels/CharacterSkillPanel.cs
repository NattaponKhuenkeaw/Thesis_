using System.Text;
using UnityEngine;
using TMPro;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterSkillPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Passive Skills")]
        [SerializeField] private TMP_Text passiveDescriptionText;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;

            if (passiveDescriptionText != null)
            {
                passiveDescriptionText.text = BuildPassiveSummary(data);
            }
        }

        private string BuildPassiveSummary(CharacterObject data)
        {
            if (data == null || data.kitDefinition == null) return "None";

            StringBuilder builder = new StringBuilder();
            AppendActionSummary(builder, data, PotentialTrack.Skill);
            AppendActionSummary(builder, data, PotentialTrack.Ultimate);
            AppendBasePassiveSummary(builder, data.kitDefinition.basePassiveNode);

            PlayerMetaProgression metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            foreach (PotentialNodeDefinition node in EnumerateUnlockedNodes(data, metaProgression))
            {
                if (node == null || node.level <= 0 || string.IsNullOrWhiteSpace(node.description))
                {
                    continue;
                }

                if (builder.Length > 0) builder.AppendLine();
                builder.Append("- ");
                builder.Append(node.track);
                builder.Append(" Lv.");
                builder.Append(node.level);
                builder.Append(": ");
                builder.Append(node.description);
            }

            return builder.Length > 0 ? builder.ToString() : "None";
        }

        private static void AppendActionSummary(StringBuilder builder, CharacterObject data, PotentialTrack track)
        {
            CombatAction action = ResolvePrimaryAction(data, track);
            if (action == null)
            {
                return;
            }

            if (builder.Length > 0) builder.AppendLine();
            builder.Append("- ");
            builder.Append(string.IsNullOrWhiteSpace(action.actionName) ? track.ToString() : action.actionName);
            builder.Append(": Inflict ");
            builder.Append("<b>");
            builder.Append(Mathf.RoundToInt(Mathf.Max(0f, action.actionDamageMultiplier) * 100f));
            builder.Append("%</b> of ATK");

            ElementType elementType = ResolveFirstActionElement(action);
            if (elementType != ElementType.None)
            {
                builder.Append(" as ");
                builder.Append(elementType);
                builder.Append(" DMG");
            }

            builder.Append(".");
        }

        private static void AppendBasePassiveSummary(StringBuilder builder, PotentialNodeDefinition basePassiveNode)
        {
            if (basePassiveNode == null)
            {
                return;
            }

            foreach (string line in BuildEffectLines(basePassiveNode))
            {
                if (builder.Length > 0) builder.AppendLine();
                builder.Append("- Passive: ");
                builder.Append(line);
            }
        }

        private static System.Collections.Generic.IEnumerable<string> BuildEffectLines(CharacterEffectSetDefinition effects)
        {
            if (effects == null)
            {
                yield break;
            }

            if (effects.defensiveEffects != null)
            {
                for (int i = 0; i < effects.defensiveEffects.Count; i++)
                {
                    PassiveDefensiveEffectDefinition effect = effects.defensiveEffects[i];
                    if (effect == null) continue;
                    if (effect.evasionChance > 0f)
                        yield return $"{Mathf.RoundToInt(effect.evasionChance * 100f)}% chance to evade incoming attacks.";
                    if (effect.grantsDamageImmunity)
                        yield return "Immune to incoming damage while active.";
                    if (effect.grantsDebuffImmunity)
                        yield return "Immune to debuffs while active.";
                }
            }

            if (effects.statModifiers != null)
            {
                for (int i = 0; i < effects.statModifiers.Count; i++)
                {
                    PassiveStatModifierDefinition modifier = effects.statModifiers[i];
                    if (modifier == null) continue;
                    string op = modifier.operation == PassiveNumericOperation.Add ? "+" : "x";
                    yield return $"{modifier.scope} {modifier.target} {op}{FormatModifierValue(modifier)}.";
                }
            }

            if (effects.additionalDamageEffects != null)
            {
                for (int i = 0; i < effects.additionalDamageEffects.Count; i++)
                {
                    AdditionalDamageEffectDefinition effect = effects.additionalDamageEffects[i];
                    if (effect == null) continue;
                    if (effect.attackRatio > 0f)
                        yield return $"{Mathf.RoundToInt(effect.chance * 100f)}% chance to deal additional {effect.elementType} DMG equal to {Mathf.RoundToInt(effect.attackRatio * 100f)}% ATK.";
                    if (effect.targetMaxHpRatio > 0f)
                        yield return $"Deal additional {effect.elementType} DMG equal to {Mathf.RoundToInt(effect.targetMaxHpRatio * 100f)}% target Max HP.";
                }
            }
        }

        private static string FormatModifierValue(PassiveStatModifierDefinition modifier)
        {
            if (modifier == null) return "0";
            if (modifier.operation == PassiveNumericOperation.Multiply)
            {
                return modifier.value.ToString("0.##");
            }

            return modifier.value.ToString("0.##");
        }

        private static CombatAction ResolvePrimaryAction(CharacterObject data, PotentialTrack track)
        {
            ActionResolverTable table = data != null ? data.actionResolverTable : null;
            if (table == null) return null;

            System.Collections.Generic.IList<CombatAction> actions = track switch
            {
                PotentialTrack.Skill => table.skillActions != null
                    ? new System.Collections.Generic.List<CombatAction>(table.skillActions)
                    : null,
                PotentialTrack.Ultimate => table.ultimateActions != null
                    ? new System.Collections.Generic.List<CombatAction>(table.ultimateActions)
                    : null,
                _ => null
            };

            if (actions == null || actions.Count == 0) return null;

            string abilityId = track switch
            {
                PotentialTrack.Skill => data.kitDefinition?.skill?.abilityId,
                PotentialTrack.Ultimate => data.kitDefinition?.ultimate?.abilityId,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(abilityId))
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    CombatAction action = actions[i];
                    if (action != null && string.Equals(action.ActionKey, abilityId, System.StringComparison.Ordinal))
                    {
                        return action;
                    }
                }
            }

            return actions[0];
        }

        private static ElementType ResolveFirstActionElement(CombatAction action)
        {
            ComboNode rootNode = action != null ? action.comboTree?.rootNode : null;
            if (rootNode == null || rootNode.stepData?.hitData?.hitboxes == null)
            {
                return ElementType.None;
            }

            for (int i = 0; i < rootNode.stepData.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = rootNode.stepData.hitData.hitboxes[i];
                ElementType elementType = hitbox switch
                {
                    NormalHitbox normalHitbox => normalHitbox.elementType,
                    GrabHitbox grabHitbox => grabHitbox.elementType,
                    _ => ElementType.None
                };

                if (elementType != ElementType.None)
                {
                    return elementType;
                }
            }

            return ElementType.None;
        }

        private static System.Collections.Generic.IEnumerable<PotentialNodeDefinition> EnumerateUnlockedNodes(
            CharacterObject data,
            PlayerMetaProgression metaProgression)
        {
            if (metaProgression != null)
            {
                return metaProgression.GetUnlockedPotentialNodes(data);
            }

            return data.kitDefinition.EnumerateAllNodes();
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
