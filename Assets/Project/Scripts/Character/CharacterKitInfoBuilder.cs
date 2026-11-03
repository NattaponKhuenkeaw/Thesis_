using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Builds reference information without creating a player or running combat routing.</summary>
public static class CharacterKitInfoBuilder
{
    private static readonly Regex Binding = new Regex(@"\{([A-Za-z][A-Za-z0-9]*)\}");

    public static CharacterKitViewModel Build(CharacterObject character, KitInfoContext context)
    {
        var sections = new List<CharacterKitViewModel.Section>();
        var diagnostics = new List<string>();
        if (character == null) return new CharacterKitViewModel(sections, diagnostics);
        context ??= new KitInfoContext(null);
        var actions = IndexActions(character.actionResolverTable, diagnostics);
        var nodes = ActiveNodes(character, context);
        var presentation = character.kitDefinition?.presentation;
        if (presentation?.entries == null || presentation.entries.Count == 0)
            presentation = CreateLegacyPresentation(character);
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var orderedSections = new List<CharacterKitPresentationDefinition.Section>(presentation.sections ?? new List<CharacterKitPresentationDefinition.Section>());
        orderedSections.RemoveAll(s => s == null);
        orderedSections.Sort((a, b) => Compare(a.order, a.sectionId, b.order, b.sectionId));
        var orderedEntries = new List<CharacterKitPresentationDefinition.Entry>(presentation.entries);
        orderedEntries.RemoveAll(e => e == null);
        orderedEntries.Sort((a, b) => Compare(a.order, a.entryId, b.order, b.entryId));
        foreach (var section in orderedSections)
        {
            if (string.IsNullOrWhiteSpace(section.sectionId) || !sectionIds.Add(section.sectionId))
            { diagnostics.Add("Missing or duplicate section ID: " + section.sectionId); continue; }
            var entries = new List<CharacterKitViewModel.Entry>();
            foreach (var definition in orderedEntries)
            {
                if (definition.sectionId != section.sectionId) continue;
                if (string.IsNullOrWhiteSpace(definition.entryId) || !entryIds.Add(definition.entryId))
                { diagnostics.Add("Missing or duplicate entry ID: " + definition.entryId); continue; }
                if (definition.hidden || Level(character, context, definition.unlockTrack) < definition.minimumPotentialLevel) continue;
                var entry = BuildEntry(character, definition, actions, nodes, context, diagnostics);
                if (entry != null && !entry.Locked) entries.Add(entry);
            }
            if (entries.Count > 0)
                sections.Add(new CharacterKitViewModel.Section(section.sectionId, section.title, section.summary,
                    SectionLevel(section, orderedEntries, character, context), entries));
        }
        foreach (var entry in orderedEntries)
            if (!sectionIds.Contains(entry.sectionId ?? string.Empty)) diagnostics.Add("Unknown section for entry: " + entry.entryId);
        ValidatePatches(character, presentation, diagnostics);
        return new CharacterKitViewModel(sections, diagnostics);
    }

    private static string SectionLevel(CharacterKitPresentationDefinition.Section section,
        List<CharacterKitPresentationDefinition.Entry> entries, CharacterObject character, KitInfoContext context)
    {
        if (section.showPotentialLevel) return "Lv." + Mathf.Clamp(Level(character, context, section.potentialTrack), 0, CharacterKitDefinition.MaxPotentialLevel);
        // Existing authored kits stored the association on their primary entry.
        foreach (var entry in entries)
            if (entry.sectionId == section.sectionId && (entry.showPotentialLevel || entry.passive))
                return "Lv." + Mathf.Clamp(Level(character, context, entry.passive ? PotentialTrack.Passive : entry.potentialTrack), 0, CharacterKitDefinition.MaxPotentialLevel);
        return string.Empty;
    }

    public static IEnumerable<CombatAction> EnumerateActions(ActionResolverTable table)
    {
        if (table == null) yield break;
        if (table.comboActions != null) foreach (var a in table.comboActions) if (a != null) yield return a;
        if (table.skillActions != null) foreach (var a in table.skillActions) if (a != null) yield return a;
        if (table.ultimateActions != null) foreach (var a in table.ultimateActions) if (a != null) yield return a;
        if (table.qteActions != null) foreach (var a in table.qteActions) if (a != null) yield return a;
        if (table.standaloneActions != null) foreach (var a in table.standaloneActions) if (a != null) yield return a;
    }

    private static Dictionary<string, CombatAction> IndexActions(ActionResolverTable table, List<string> diagnostics)
    {
        var result = new Dictionary<string, CombatAction>(StringComparer.Ordinal);
        foreach (var action in EnumerateActions(table))
        {
            if (string.IsNullOrWhiteSpace(action.actionId)) diagnostics.Add("Action needs a stable ID: " + action.actionName);
            string key = action.ActionKey;
            if (string.IsNullOrWhiteSpace(key)) { diagnostics.Add("Action has no key."); continue; }
            if (!result.TryAdd(key, action)) diagnostics.Add("Duplicate action ID: " + key);
        }
        return result;
    }

    private static IReadOnlyList<PotentialNodeDefinition> ActiveNodes(CharacterObject character, KitInfoContext context)
    {
        if (context.Progression != null) return context.Progression.GetUnlockedPotentialNodes(character);
        var nodes = new List<PotentialNodeDefinition>();
        if (character.kitDefinition != null)
            foreach (var node in character.kitDefinition.EnumerateAllNodes())
                if (node != null && (node.unlockedByDefault || node.level <= 0)) nodes.Add(node);
        return nodes.AsReadOnly();
    }

    private static CharacterKitViewModel.Entry BuildEntry(CharacterObject character,
        CharacterKitPresentationDefinition.Entry definition, Dictionary<string, CombatAction> actions,
        IReadOnlyList<PotentialNodeDefinition> nodes, KitInfoContext context, List<string> diagnostics)
    {
        var kit = character.kitDefinition;
        if (definition.passive)
        {
            string copy = kit?.passive?.description;
            if (!HasPatches(kit, definition.entryId)) copy = LegacyDescription(nodes, PotentialTrack.Passive) ?? copy;
            copy = ApplyPatches(copy, definition.entryId, nodes, diagnostics);
            copy = Bind(copy, new Dictionary<string, string>(), definition.entryId, diagnostics);
            return new CharacterKitViewModel.Entry(definition.entryId, null, kit?.passive?.abilityName ?? "Passive",
                kit?.passive?.icon, string.Empty,
                string.IsNullOrWhiteSpace(copy) ? "No passive effect." : copy, false);
        }
        if (!actions.TryGetValue(definition.actionId ?? string.Empty, out var action))
        { diagnostics.Add("Unknown action for entry: " + definition.entryId + " -> " + definition.actionId); return null; }
        if (!definition.qte && action is ComboAction normal && character.actionResolverTable?.comboActions?.Contains(normal) == true)
            return null;
        if (definition.qte && (character.qteSwap == null || !character.qteSwap.enabled)) return null;
        if (definition.qte && !string.Equals(action.ActionKey, character.qteSwap.introActionKey, StringComparison.Ordinal))
        { diagnostics.Add("QTE entry does not match introActionKey: " + definition.entryId); return null; }
        if (definition.qte && !string.IsNullOrWhiteSpace(character.qteSwap.introStepName)
            && !HasStep(action.comboTree?.rootNode, character.qteSwap.introStepName, new HashSet<ComboNode>()))
            diagnostics.Add("Unknown QTE intro step for entry: " + definition.entryId);

        bool reference = context.RuntimeState == null;
        float damage = ActionModifierEvaluation.Evaluate(nodes, ActionModifierEvaluation.Value.DamageMultiplier,
            action.Category, action.actionId, action.actionName, action.actionDamageMultiplier, context.RuntimeState, reference);
        float cooldown = action is SkillAction skill && skill.cooldown > 0f
            ? ActionModifierEvaluation.Evaluate(nodes, ActionModifierEvaluation.Value.Cooldown,
                action.Category, action.actionId, action.actionName, skill.cooldown, context.RuntimeState, reference) : 0f;
        float energy = action is UltimateAction ultimate
            ? ActionModifierEvaluation.Evaluate(nodes, ActionModifierEvaluation.Value.EnergyCost,
                action.Category, action.actionId, action.actionName, ultimate.energyCost, context.RuntimeState, reference) : 0f;
        var metadata = new List<string>();
        var tags = new List<CharacterKitViewModel.HeaderTag>();
        var notes = new List<string>();
        bool locked = false;
        string replacementId = null;
        string replacementName = null;
        tags.Add(action.context == CombatActionContext.Ground
            ? new CharacterKitViewModel.HeaderTag(CharacterKitViewModel.TagKind.Ground, "Ground")
            : action.context == CombatActionContext.Air
                ? new CharacterKitViewModel.HeaderTag(CharacterKitViewModel.TagKind.Airborne, "Airborne")
                : new CharacterKitViewModel.HeaderTag(CharacterKitViewModel.TagKind.Both, "Both"));
        if (cooldown > 0) tags.Add(new CharacterKitViewModel.HeaderTag(CharacterKitViewModel.TagKind.Cooldown, Number(cooldown) + "s"));
        if (action is UltimateAction) tags.Add(new CharacterKitViewModel.HeaderTag(CharacterKitViewModel.TagKind.Energy, Number(energy) + " AE"));
        string source = Source(action.damageSourceStat);
        foreach (var node in nodes)
        {
            if (node.actionModifiers == null) continue;
            foreach (var modifier in node.actionModifiers)
            {
                if (modifier == null) continue;
                // Step-scoped values cannot truthfully be folded into a whole-action scalar.
                string step = modifier.stepName;
                if (!modifier.Matches(action.Category, action.actionId, action.actionName, step, null, true)) continue;
                if (ActionModifierEvaluation.IsConditional(modifier) || !string.IsNullOrWhiteSpace(step))
                {
                    continue;
                }
                if (!ActionModifierEvaluation.Matches(modifier, action.Category, action.actionId, action.actionName, context.RuntimeState, reference)) continue;
                locked = !modifier.unlocked;
                if (!string.IsNullOrWhiteSpace(modifier.replacementActionId)) replacementId = modifier.replacementActionId;
                if (!string.IsNullOrWhiteSpace(modifier.replacementActionName)) replacementName = modifier.replacementActionName;
            }
        }
        if ((!string.IsNullOrEmpty(replacementId) && replacementId != action.actionId)
            || (!string.IsNullOrEmpty(replacementName) && replacementName != action.actionName))
        {
            locked = true;
            string replacement = replacementName;
            if (!string.IsNullOrEmpty(replacementId))
            {
                if (actions.TryGetValue(replacementId, out var replaced)) replacement = replaced.actionName;
                else { diagnostics.Add("Unknown replacement action: " + replacementId); replacement = replacementId; }
            }
            notes.Add("Replaced by " + replacement + ".");
        }
        if (action.conditions != null)
            foreach (var condition in action.conditions)
            {
                if (condition is PotentialLevelActionCondition tier)
                {
                    if (!ActionConditionUtility.EvaluateComparison(Level(character, context, tier.track), tier.comparison, tier.level)) locked = true;
                }
            }
        if (locked) metadata.Insert(0, "Locked / replaced");
        string activation = !string.IsNullOrWhiteSpace(definition.activationDescription)
            ? definition.activationDescription : definition.qte ? null : action.activationDescription;
        if (!string.IsNullOrWhiteSpace(activation)) notes.Insert(0, activation);
        string description = action.description;
        if (string.IsNullOrWhiteSpace(description) && IsPrimary(character.actionResolverTable, action))
        {
            var track = action.Category == AttackCategory.Ultimate ? PotentialTrack.Ultimate : PotentialTrack.Skill;
            var ability = track == PotentialTrack.Ultimate ? kit?.ultimate : kit?.skill;
            description = HasPatches(kit, definition.entryId) ? ability?.description : LegacyDescription(nodes, track) ?? ability?.description;
        }
        description = ApplyPatches(description, definition.entryId, nodes, diagnostics);
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cooldown"] = Number(cooldown) + "s", ["energyCost"] = Number(energy),
            ["multiplierPercent"] = Number(damage * 100f) + "%", ["sourceStat"] = source
        };
        description = Bind(description, bindings, definition.entryId, diagnostics);
        for (int i = 0; i < notes.Count; i++) notes[i] = Bind(notes[i], bindings, definition.entryId, diagnostics);
        if (notes.Count > 0) description = Join(description, string.Join("\n", notes));
        string title = string.IsNullOrWhiteSpace(action.actionName)
            ? (action.Category == AttackCategory.Normal ? "Basic Attack" : action.Category.ToString()) : action.actionName;
        Sprite icon = action.icon;
        if (icon == null && IsPrimary(character.actionResolverTable, action))
            icon = action.Category == AttackCategory.Ultimate ? kit?.ultimate?.icon : kit?.skill?.icon;
        return new CharacterKitViewModel.Entry(definition.entryId, action.actionId, title, icon,
            string.Join(" / ", metadata), description ?? string.Empty, locked, tags);
    }

    private static string ApplyPatches(string original, string entryId, IReadOnlyList<PotentialNodeDefinition> nodes, List<string> diagnostics)
    {
        var replacements = new List<CharacterKitPresentationDefinition.Patch>();
        var notes = new List<CharacterKitPresentationDefinition.Patch>();
        foreach (var node in nodes)
            if (node.kitInfoPatches != null)
                foreach (var patch in node.kitInfoPatches)
                    if (patch != null && patch.entryId == entryId && !string.IsNullOrWhiteSpace(patch.description))
                        (patch.appendNote ? notes : replacements).Add(patch);
        replacements.Sort((a, b) => b.priority.CompareTo(a.priority));
        if (replacements.Count > 1 && replacements[0].priority == replacements[1].priority)
            diagnostics.Add("Conflicting description replacements: " + entryId);
        else if (replacements.Count > 0) original = replacements[0].description;
        notes.Sort((a, b) => Compare(a.priority, a.noteId, b.priority, b.noteId));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var note in notes)
        {
            if (string.IsNullOrWhiteSpace(note.noteId) || !ids.Add(note.noteId))
            { diagnostics.Add("Missing or duplicate note ID for entry: " + entryId); continue; }
            original = Join(original, note.description);
        }
        return original;
    }

    private static void ValidatePatches(CharacterObject character, CharacterKitPresentationDefinition presentation, List<string> diagnostics)
    {
        if (character.kitDefinition == null) return;
        var ids = new HashSet<string>();
        foreach (var entry in presentation.entries) if (entry != null) ids.Add(entry.entryId ?? string.Empty);
        foreach (var node in character.kitDefinition.EnumerateAllNodes())
            if (node.kitInfoPatches != null)
                foreach (var patch in node.kitInfoPatches)
                    if (patch != null && !ids.Contains(patch.entryId ?? string.Empty)) diagnostics.Add("Unknown patch target: " + patch.entryId);
    }

    private static bool HasPatches(CharacterKitDefinition kit, string entryId)
    {
        if (kit == null) return false;
        foreach (var node in kit.EnumerateAllNodes())
            if (node.kitInfoPatches != null && node.kitInfoPatches.Exists(p => p != null && p.entryId == entryId)) return true;
        return false;
    }

    private static string LegacyDescription(IReadOnlyList<PotentialNodeDefinition> nodes, PotentialTrack track)
    {
        PotentialNodeDefinition best = null;
        foreach (var node in nodes)
            if (node.track == track && !string.IsNullOrWhiteSpace(node.skillPanelDescriptionOverride)
                && (best == null || node.level >= best.level)) best = node;
        return best?.skillPanelDescriptionOverride;
    }

    private static bool IsPrimary(ActionResolverTable table, CombatAction action) => table != null
        && ((table.skillActions?.Count > 0 && ReferenceEquals(table.skillActions[0], action))
            || (table.ultimateActions?.Count > 0 && ReferenceEquals(table.ultimateActions[0], action)));

    private static int Level(CharacterObject character, KitInfoContext context, PotentialTrack track) =>
        Mathf.Clamp(context.Progression != null ? context.Progression.GetPotentialLevel(character, track) : 0,
            0, CharacterKitDefinition.MaxPotentialLevel);

    private static bool HasStep(ComboNode node, string name, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return false;
        if (node.stepData?.stepName == name) return true;
        if (HasStep(node.defaultNext, name, visited) || HasStep(node.onGrabSuccessNode, name, visited)) return true;
        if (node.routes != null) foreach (var route in node.routes) if (HasStep(route?.targetNode, name, visited)) return true;
        return false;
    }

    private static string Bind(string copy, Dictionary<string, string> values, string entryId, List<string> diagnostics)
    {
        return Binding.Replace(copy ?? string.Empty, match =>
        {
            if (values.TryGetValue(match.Groups[1].Value, out string value)) return value;
            diagnostics.Add("Unknown binding " + match.Value + " in " + entryId);
            return match.Value;
        });
    }

    public static CharacterKitPresentationDefinition CreateLegacyPresentation(CharacterObject character)
    {
        var result = new CharacterKitPresentationDefinition();
        string[] ids = { "skill", "ultimate", "passive", "special", "qte" };
        string[] titles = { "Skill", "Ultimate", "Passive", "Special", "QTE" };
        for (int i = 0; i < ids.Length; i++) result.sections.Add(new CharacterKitPresentationDefinition.Section
        { sectionId = ids[i], title = titles[i], order = i, showPotentialLevel = i == 0 || i == 1 || i == 2,
            potentialTrack = i == 2 ? PotentialTrack.Passive : i == 1 ? PotentialTrack.Ultimate : PotentialTrack.Skill });
        var table = character.actionResolverTable;
        if (table != null)
        {
            AddEntries(result, "skill", table.skillActions);
            AddEntries(result, "ultimate", table.ultimateActions);
            AddEntries(result, "special", table.standaloneActions);
            if (character.qteSwap != null && character.qteSwap.enabled)
            {
                foreach (var action in EnumerateActions(table))
                    if (action.ActionKey == character.qteSwap.introActionKey)
                    {
                        result.entries.Add(new CharacterKitPresentationDefinition.Entry
                        { entryId = "qte." + action.ActionKey, sectionId = "qte", actionId = action.ActionKey, qte = true });
                        break;
                    }
            }
        }
        if (character.kitDefinition != null)
            result.entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = "passive", sectionId = "passive", passive = true });
        return result;
    }

    private static void AddEntries<T>(CharacterKitPresentationDefinition result, string section, List<T> actions) where T : CombatAction
    {
        if (actions == null) return;
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] != null) result.entries.Add(new CharacterKitPresentationDefinition.Entry
            { entryId = actions[i].ActionKey, sectionId = section, actionId = actions[i].ActionKey, order = i,
                showPotentialLevel = i == 0 && (section == "skill" || section == "ultimate"),
                potentialTrack = section == "ultimate" ? PotentialTrack.Ultimate : PotentialTrack.Skill });
    }

    private static int Compare(int orderA, string idA, int orderB, string idB) => orderA != orderB
        ? orderA.CompareTo(orderB) : string.CompareOrdinal(idA, idB);
    private static string Join(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a.Trim() + "\n\n" + b;
    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Source(ActionDamageSourceStat source) => source == ActionDamageSourceStat.MaxHP ? "Max HP" : source == ActionDamageSourceStat.Defense ? "DEF" : "ATK";
}
