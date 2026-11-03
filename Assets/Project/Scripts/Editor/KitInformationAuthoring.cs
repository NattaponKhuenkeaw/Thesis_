using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, repeatable migration of presentation fields; leaves execution payloads intact.</summary>
public static class KitInformationAuthoring
{
    private const string HeroRoot = "Assets/Project/Character/Hero";

    [MenuItem("Tools/Project/Kit Information/Migrate Character Presentation")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Migrate outside Play Mode.");
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterObject", new[] { HeroRoot }))
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (character == null || character.actionResolverTable == null || character.kitDefinition == null) continue;
            var table = character.actionResolverTable;
            if (character.kitDefinition.presentation?.entries?.Count > 0)
            {
                Undo.RecordObjects(new UnityEngine.Object[] { character, table }, "Restore Authored Kit Copy");
                bool changed = RestoreGeneratedCopy(character);
                changed |= MigrateLegacyMeterGainRuleKeys(character);
                if (!changed) continue;
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(character);
                AssetDatabase.SaveAssetIfDirty(table);
                AssetDatabase.SaveAssetIfDirty(character);
                count++;
                continue;
            }
            Undo.RecordObjects(new UnityEngine.Object[] { character, table }, "Migrate Kit Information");
            MigrateCharacter(character);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(table);
            AssetDatabase.SaveAssetIfDirty(character);
            count++;
        }
        Debug.Log("Kit information presentation updated: " + count + " character(s).");
    }

    private static bool RestoreGeneratedCopy(CharacterObject character)
    {
        var kit = character.kitDefinition;
        var table = character.actionResolverTable;
        bool changed = false;
        foreach (var node in kit.EnumerateAllNodes())
        {
            if (node?.kitInfoPatches == null) continue;
            foreach (var patch in node.kitInfoPatches)
            {
                if (patch == null || patch.appendNote || !HasGeneratedDamagePhrase(patch.description)) continue;
                patch.description = node.skillPanelDescriptionOverride ?? string.Empty;
                changed = true;
            }
        }
        var skill = table.skillActions?.FirstOrDefault();
        if (skill != null && HasGeneratedDamagePhrase(skill.description))
        {
            skill.description = kit.baseSkillNode?.skillPanelDescriptionOverride ?? kit.skill?.description ?? string.Empty;
            changed = true;
        }
        var ultimate = table.ultimateActions?.FirstOrDefault();
        if (ultimate != null && HasGeneratedDamagePhrase(ultimate.description))
        {
            ultimate.description = kit.baseUltimateNode?.skillPanelDescriptionOverride ?? kit.ultimate?.description ?? string.Empty;
            changed = true;
        }
        return changed;
    }

    private static bool HasGeneratedDamagePhrase(string copy) =>
        !string.IsNullOrEmpty(copy) && (copy.Contains("Action scaling {multiplierPercent} of {sourceStat}")
            || copy.Contains("Inflict DMG {multiplierPercent} of {sourceStat}"));

    private static bool MigrateLegacyMeterGainRuleKeys(CharacterObject character)
    {
        if (character?.combatRules?.meterGainRules == null || character.actionResolverTable == null) return false;
        var actions = CharacterKitInfoBuilder.EnumerateActions(character.actionResolverTable)
            .Where(action => action != null && !string.IsNullOrWhiteSpace(action.actionId)).ToList();
        bool changed = false;
        foreach (var rule in character.combatRules.meterGainRules)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.actionKey)
                || actions.Exists(action => string.Equals(action.actionId, rule.actionKey, StringComparison.Ordinal))) continue;
            var matches = actions.Where(action => string.Equals(action.actionName, rule.actionKey, StringComparison.Ordinal)).ToList();
            if (matches.Count != 1) continue;
            rule.actionKey = matches[0].actionId;
            changed = true;
        }
        return changed;
    }

    public static void MigrateCharacter(CharacterObject character)
    {
        var table = character.actionResolverTable;
        var kit = character.kitDefinition;
        if (table == null || kit == null || kit.presentation?.entries?.Count > 0) return;
        AssignIds(character.characterId, "basic", table.comboActions);
        AssignIds(character.characterId, "skill", table.skillActions);
        AssignIds(character.characterId, "ultimate", table.ultimateActions);
        AssignIds(character.characterId, "qte", table.qteActions);
        AssignIds(character.characterId, "special", table.standaloneActions);
        MigrateLegacyMeterGainRuleKeys(character);
        // QTE's legacy name reference remains valid after assigning the action a stable ID.
        if (character.qteSwap != null && character.qteSwap.enabled)
        {
            var intro = CharacterKitInfoBuilder.EnumerateActions(table).FirstOrDefault(a =>
                a.actionId == character.qteSwap.introActionKey || a.actionName == character.qteSwap.introActionKey);
            if (intro != null) character.qteSwap.introActionKey = intro.actionId;
        }
        kit.presentation = CharacterKitInfoBuilder.CreateLegacyPresentation(character);
        foreach (var entry in kit.presentation.entries)
        {
            // These follow-ups are granted by Potential effects, rather than a direct action-level condition.
            if (entry.actionId == "rugal.skill.ex") { entry.minimumPotentialLevel = 4; entry.unlockTrack = PotentialTrack.Passive; }
            if (entry.actionId == "cg.skill.ex") { entry.minimumPotentialLevel = 1; entry.unlockTrack = PotentialTrack.Ultimate; }
        }
        var skill = table.skillActions?.FirstOrDefault();
        var ultimate = table.ultimateActions?.FirstOrDefault();
        if (skill != null) { skill.description = ActionCopy(kit.baseSkillNode, kit.skill); if (skill.icon == null) skill.icon = kit.skill?.icon; }
        if (ultimate != null) { ultimate.description = ActionCopy(kit.baseUltimateNode, kit.ultimate); if (ultimate.icon == null) ultimate.icon = kit.ultimate?.icon; }
        if (kit.passive != null && !string.IsNullOrWhiteSpace(kit.basePassiveNode?.skillPanelDescriptionOverride))
            kit.passive.description = kit.basePassiveNode.skillPanelDescriptionOverride;
        foreach (var action in CharacterKitInfoBuilder.EnumerateActions(table))
            if (string.IsNullOrWhiteSpace(action.description)) action.description = "Perform "
                + (string.IsNullOrWhiteSpace(action.actionName) ? "a basic attack" : action.actionName) + ".";

        foreach (var node in kit.EnumerateAllNodes())
        {
            if (string.IsNullOrWhiteSpace(node.skillPanelDescriptionOverride)) continue;
            string entryId = node.track == PotentialTrack.Passive ? "passive"
                : node.track == PotentialTrack.Skill ? skill?.actionId : ultimate?.actionId;
            if (string.IsNullOrWhiteSpace(entryId)) continue;
            node.kitInfoPatches ??= new List<CharacterKitPresentationDefinition.Patch>();
            if (node.kitInfoPatches.Exists(p => p != null && p.entryId == entryId && !p.appendNote)) continue;
            node.kitInfoPatches.Add(new CharacterKitPresentationDefinition.Patch
            {
                entryId = entryId, priority = node.level,
                description = node.track == PotentialTrack.Passive ? node.skillPanelDescriptionOverride : BindActionScaling(node.skillPanelDescriptionOverride)
            });
        }
        if (character.characterId == "char-0-main") AuthorMain(character);
    }

    private static void AssignIds<T>(string characterId, string group, List<T> actions) where T : CombatAction
    {
        if (actions == null) return;
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] != null && string.IsNullOrWhiteSpace(actions[i].actionId))
                actions[i].actionId = characterId + "." + group + "." + i;
    }

    private static string ActionCopy(PotentialNodeDefinition node, CharacterAbilityDefinition ability) =>
        BindActionScaling(!string.IsNullOrWhiteSpace(node?.skillPanelDescriptionOverride) ? node.skillPanelDescriptionOverride : ability?.description);

    private static string BindActionScaling(string copy)
    {
        // Kit descriptions remain authored copy; runtime-scaled damage is not injected into prose.
        return copy ?? string.Empty;
    }

    private static void AuthorMain(CharacterObject character)
    {
        foreach (var action in CharacterKitInfoBuilder.EnumerateActions(character.actionResolverTable))
            switch (action.actionId)
            {
                case "main.skill.dimension_slash":
                    action.description = "Perform Judgment Cut.";
                    break;
                case "main.ultimate.execution_breakdown":
                    action.description = "Perform Execution Breakdown.";
                    break;
                case "main.qte.lightning_plunge":
                    action.description = "Enter with a lightning plunge and strike on landing.";
                    break;
                case "main.dash.dimension_cut":
                    action.description = "Dash through the enemy with a Dimension Cut.";
                    action.activationDescription = "After dashing to cache a cut, use Normal Attack to spend a cached cut.";
                    break;
            }
    }
}
