using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class PlayerStatAssetMigrator
{
    public const int MigrationVersion = 2;

    private static readonly Regex AuraRechargePattern = new Regex(
        @"^(?<indent>\s*)aeRecharge:\s*(?<value>-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex DamageReductionPattern = new Regex(
        @"^\s*damageReduction:\s*(?<value>-?\d+(?:\.\d+)?)\s*\r?\n?",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [MenuItem("Tools/Combat/Player Stats/1. Dry Run Migration v2")]
    public static void DryRun()
    {
        Run(false);
    }

    [MenuItem("Tools/Combat/Player Stats/2. Apply Migration v2")]
    public static void Apply()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Player Stat Migration v2",
                "Rename legacy stat keys, convert decimal AE Recharge to percentage points, and remove character Damage Reduction fields? Review the dry-run Console report first.",
                "Apply",
                "Cancel"))
        {
            return;
        }

        Run(true);
    }

    private static void Run(bool apply)
    {
        string[] assetPaths = AssetDatabase.GetAllAssetPaths();
        List<string> changedPaths = new List<string>();
        List<string> clearedDamageReduction = new List<string>();

        for (int i = 0; i < assetPaths.Length; i++)
        {
            string path = assetPaths[i];
            if (!IsTextAsset(path) || !File.Exists(path))
            {
                continue;
            }

            string original = File.ReadAllText(path);
            string migrated = RenameLegacyKeys(original);
            migrated = AuraRechargePattern.Replace(migrated, match =>
            {
                float legacyValue = float.Parse(
                    match.Groups["value"].Value,
                    CultureInfo.InvariantCulture);
                return match.Groups["indent"].Value
                    + "auraEnergyRecharge: "
                    + (legacyValue * 100f).ToString("0.####", CultureInfo.InvariantCulture);
            });
            migrated = DamageReductionPattern.Replace(migrated, match =>
            {
                float value = float.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
                if (Mathf.Abs(value) > 0.0001f)
                {
                    clearedDamageReduction.Add($"{path} ({value.ToString(CultureInfo.InvariantCulture)})");
                }
                return string.Empty;
            });

            if (migrated == original)
            {
                continue;
            }

            changedPaths.Add(path);
            if (apply)
            {
                File.WriteAllText(path, migrated, new UTF8Encoding(false));
            }
        }

        if (apply && changedPaths.Count > 0)
        {
            AssetDatabase.Refresh();
        }

        StringBuilder report = new StringBuilder();
        report.Append("Player Stat Migration v").Append(MigrationVersion)
            .Append(apply ? " applied" : " dry run")
            .Append(": ").Append(changedPaths.Count).Append(" asset(s) require changes.");
        if (clearedDamageReduction.Count > 0)
        {
            report.AppendLine().Append("Non-zero Damage Reduction values cleared without ADI mapping:")
                .AppendLine().Append(string.Join("\n", clearedDamageReduction));
        }
        Debug.Log(report.ToString());
    }

    private static string RenameLegacyKeys(string yaml)
    {
        string migrated = Regex.Replace(
            yaml,
            @"^(?<indent>\s*)elementalMastery:",
            "${indent}dotMastery:",
            RegexOptions.Multiline);
        migrated = Regex.Replace(
            migrated,
            @"^(?<indent>\s*)cooldownRate:",
            "${indent}cooldownMastery:",
            RegexOptions.Multiline);
        return Regex.Replace(
            migrated,
            @"^(?<indent>\s*)impact:",
            "${indent}damageIncrease:",
            RegexOptions.Multiline);
    }

    private static bool IsTextAsset(string path)
    {
        return path.StartsWith("Assets/")
            && (path.EndsWith(".asset") || path.EndsWith(".prefab") || path.EndsWith(".unity"));
    }
}
