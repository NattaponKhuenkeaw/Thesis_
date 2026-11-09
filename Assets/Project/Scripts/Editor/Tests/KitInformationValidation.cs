using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Progression;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Panels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class KitInformationValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "Docs_JJK/3_Outputs/Reports/KitInformation";
    [Serializable] private sealed class Report
    {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }

    private sealed class Fixture : IDisposable
    {
        public readonly CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        public readonly ActionResolverTable table = ScriptableObject.CreateInstance<ActionResolverTable>();
        public readonly CharacterProgressionState progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
        public readonly UltimateAction tap = new UltimateAction { actionId = "tap", actionName = "Tap", description = "Tap {multiplierPercent}", energyCost = 60, cooldown = 10, actionDamageMultiplier = 5 };
        public readonly UltimateAction hold = new UltimateAction { actionId = "hold", actionName = "Hold", description = "Hold copy", inputTrigger = UltimateInputTrigger.Hold, energyCost = 0 };
        public Fixture()
        {
            character.characterId = "kit-fixture";
            character.actionResolverTable = table;
            table.ultimateActions.Add(tap); table.ultimateActions.Add(hold);
            table.skillActions.Add(new SkillAction { actionId = "skill", actionName = "Skill", actionDamageMultiplier = 2 });
            character.kitDefinition.presentation = CharacterKitInfoBuilder.CreateLegacyPresentation(character);
        }
        public CharacterKitViewModel Build(CombatRuntimeState state = null) => CharacterKitInfoBuilder.Build(character, new KitInfoContext(progression, state));
        public CharacterKitViewModel.Entry Entry(string id, CombatRuntimeState state = null) => Build(state).Sections.SelectMany(s => s.Entries).Single(e => e.Id == id);
        public PotentialNodeDefinition Node(PotentialTrack track, int level = 0)
        {
            var node = new PotentialNodeDefinition { nodeId = Guid.NewGuid().ToString(), nodeName = "Fixture upgrade", track = track, level = level };
            character.kitDefinition.potentialNodes.Add(node);
            return node;
        }
        public void Dispose() { Object.DestroyImmediate(progression); Object.DestroyImmediate(table); Object.DestroyImmediate(character); }
    }

    [MenuItem("Tools/Project/Validation/Run Kit Information Checks")]
    public static void Run()
    {
        var report = new Report();
        Check(report, "Independent Ultimates and verified bindings", () => {
            using var f = new Fixture();
            Require(f.Build().Sections.Single(s => s.Id == "ultimate").Entries.Count == 2, "Both actions must appear");
            Require(f.Entry("tap").Description.StartsWith("Tap 500%"), "Typed scaling binding");
            Require(f.Entry("hold").Description.Contains("Hold copy") && !f.Entry("hold").Description.Contains("Tap"), "No whole-track leakage");
            Require(f.Entry("hold").HeaderTags.Any(t => t.Kind == CharacterKitViewModel.TagKind.Energy && t.Label == "0 AE") && !f.Entry("hold").HeaderTags.Any(t => t.Kind == CharacterKitViewModel.TagKind.Cooldown), "Zero-cost/cooldown follow-up");
        });
        Check(report, "Cross-track upgrades and ordered dedicated cooldown modifiers", () => {
            using var f = new Fixture();
            var node = f.Node(PotentialTrack.Passive);
            node.cooldownModifiers.Add(new PassiveCooldownModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", operation = PassiveNumericOperation.Multiply, value = .5f });
            node.actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", damageMultiplierOperation = PassiveValueModifierOperation.Add, damageMultiplierValue = 2,
                cooldownOperation = PassiveValueModifierOperation.Add, cooldownValue = 1, energyCostOperation = PassiveValueModifierOperation.Multiply, energyCostValue = .5f });
            var entry = f.Entry("tap");
            Require(entry.Description.Contains("700%"), "Passive-track modifier affects Ultimate");
            Require(entry.HeaderTags.Any(t => t.Kind == CharacterKitViewModel.TagKind.Cooldown && t.Label == "6s") && entry.HeaderTags.Any(t => t.Kind == CharacterKitViewModel.TagKind.Energy && t.Label == "30 AE"), "Modifier order and costs");
        });
        Check(report, "Unknown positive and negative combat requirements stay conditional", () => {
            using var f = new Fixture();
            var node = f.Node(PotentialTrack.Passive);
            node.actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", damageMultiplierOperation = PassiveValueModifierOperation.Multiply, damageMultiplierValue = 2,
                requiredTags = new List<RuntimeTagRequirement> { new RuntimeTagRequirement { tag = "burst", expectedValue = false } } });
            Require(f.Entry("tap").Description.StartsWith("Tap 500%"), "Absent runtime state is unknown, not a negative-tag match");
            Require(!f.Entry("tap").Description.Contains("Conditional upgrade"), "No generated conditional note");
            Require(f.Entry("tap", new CombatRuntimeState()).Description.StartsWith("Tap 1000%"), "Explicit preview can evaluate the negative tag");
        });
        Check(report, "Step-only upgrades cannot inflate whole-action scaling", () => {
            using var f = new Fixture();
            f.Node(PotentialTrack.Skill).actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", stepName = "Finisher", damageMultiplierOperation = PassiveValueModifierOperation.Override, damageMultiplierValue = 99 });
            Require(f.Entry("tap").Description.StartsWith("Tap 500%") && !f.Entry("tap").Description.Contains("Conditional upgrade"), "Step restriction does not generate a note");
        });
        Check(report, "Per-entry patches across tracks and tier changes", () => {
            using var f = new Fixture();
            f.Node(PotentialTrack.Passive, 1).kitInfoPatches.Add(new CharacterKitPresentationDefinition.Patch { entryId = "hold", description = "Upgraded hold", priority = 1 });
            Require(!f.Entry("hold").Description.Contains("Upgraded"), "Locked node cannot override");
            f.progression.SetPotentialLevel(f.character, PotentialTrack.Passive, 1);
            Require(f.Entry("hold").Description.Contains("Upgraded hold") && f.Entry("tap").Description.Contains("Tap 500%"), "Patch only affects target");
        });
        Check(report, "Conflicts and invalid bindings produce authoring diagnostics", () => {
            using var f = new Fixture();
            var patches = f.Node(PotentialTrack.Skill).kitInfoPatches;
            patches.Add(new CharacterKitPresentationDefinition.Patch { entryId = "tap", priority = 1, description = "First" });
            patches.Add(new CharacterKitPresentationDefinition.Patch { entryId = "tap", priority = 1, description = "Second" });
            patches.Add(new CharacterKitPresentationDefinition.Patch { entryId = "missing", description = "Bad target" });
            f.hold.description = "{notAValue}";
            var model = f.Build();
            Require(model.Diagnostics.Any(d => d.Contains("Conflicting")) && model.Diagnostics.Any(d => d.Contains("Unknown patch")) && model.Diagnostics.Any(d => d.Contains("Unknown binding")), "All invalid authoring reported");
            Require(f.Entry("tap").Description.StartsWith("Tap 500%"), "Conflicting copy does not depend on list order");
        });
        Check(report, "QTE reuses an ordinary action without inventing a track", () => {
            using var f = new Fixture();
            f.character.qteSwap.enabled = true; f.character.qteSwap.introActionKey = "tap";
            f.tap.activationDescription = "Ordinary Ultimate activation";
            f.character.kitDefinition.presentation = CharacterKitInfoBuilder.CreateLegacyPresentation(f.character);
            var entry = f.Entry("qte.tap");
            Require(entry.ActionId == "tap" && !entry.Metadata.Contains("Ultimate Potential"), "Separate usage entry, same execution action");
            Require(!entry.Description.Contains("Prepared team swap"), "No generated QTE activation phrase");
            Require(!entry.Description.Contains("Ordinary Ultimate activation"), "Reused action does not inherit ordinary activation instructions");
        });
        Check(report, "Standalone action placement is independent of combat category", () => {
            using var f = new Fixture();
            f.table.standaloneActions.Add(new StandaloneAction { actionId = "cut", actionName = "Cut", triggerCategory = AttackCategory.Normal });
            f.character.kitDefinition.presentation = CharacterKitInfoBuilder.CreateLegacyPresentation(f.character);
            Require(f.Build().Sections.Single(s => s.Id == "special").Entries.Single().ActionId == "cut", "Normal-input standalone appears under Special");
        });
        Check(report, "Permanent locks and replacement state follow gameplay modifiers", () => {
            using var f = new Fixture();
            var node = f.Node(PotentialTrack.Passive);
            node.actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", replacementActionId = "hold" });
            node.actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", unlocked = true });
            Require(f.Build().Sections.SelectMany(s => s.Entries).All(e => e.Id != "tap"), "Permanently replaced action is filtered out");
        });
        Check(report, "Base actions at tier zero are not labeled inactive", () => {
            using var f = new Fixture(); Require(!f.Entry("tap").Locked && !f.Entry("tap").Metadata.Contains("Inactive"), "Base kit works at tier zero");
        });
        Check(report, "Missing action/section and duplicate action IDs are diagnosed", () => {
            using var f = new Fixture();
            f.table.ultimateActions.Add(new UltimateAction { actionId = "tap" });
            f.character.kitDefinition.presentation.entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = "bad", actionId = "absent", sectionId = "ultimate" });
            f.character.kitDefinition.presentation.entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = "orphan", sectionId = "absent" });
            Require(f.Build().Diagnostics.Count >= 3, "Invalid references reported without a crash");
        });
        Check(report, "Inspection never changes authored action or character data", () => {
            using var f = new Fixture();
            string character = EditorJsonUtility.ToJson(f.character), table = EditorJsonUtility.ToJson(f.table);
            f.Build(); f.Build();
            Require(character == EditorJsonUtility.ToJson(f.character) && table == EditorJsonUtility.ToJson(f.table), "Read-only build");
        });
        Check(report, "Migration is repeatable and preserves execution values", () => {
            using var f = new Fixture();
            f.character.kitDefinition.presentation.entries.Clear();
            f.character.kitDefinition.baseUltimateNode.skillPanelDescriptionOverride = "Inflict DMG <b><color=#a382ed>500%</color></b> of ATK";
            KitInformationAuthoring.MigrateCharacter(f.character);
            string first = EditorJsonUtility.ToJson(f.character);
            KitInformationAuthoring.MigrateCharacter(f.character);
            Require(first == EditorJsonUtility.ToJson(f.character) && f.tap.energyCost == 60 && f.tap.cooldown == 10 && f.tap.actionDamageMultiplier == 5, "Repeatable presentation-only migration");
            Require(f.tap.description.Contains("{multiplierPercent}"), "Literal action percentage migrated to a binding");
        });
        Check(report, "Runtime controller uses shared values with existing clamps", RuntimeParity);
        Check(report, "Potential-gated actions appear at the exact unlock level and survive runtime inactivity", () => {
            using var f = new Fixture();
            f.hold.conditions.Add(new PotentialLevelActionCondition { track = PotentialTrack.Passive, level = 4 });
            f.hold.conditions.Add(new RuntimeTagActionCondition { tag = "ready" });
            for (int level = 0; level <= 4; level++)
            {
                f.progression.SetPotentialLevel(f.character, PotentialTrack.Passive, level);
                var model = f.Build();
                Require(model.Sections.SelectMany(s => s.Entries).Any(e => e.Id == "hold") == (level == 4), "Exact Passive unlock boundary");
                Require(model.Sections.Single(s => s.Id == "ultimate").LevelLabel == "Lv.0", "Unlock track does not change root's upgrade track");
            }
            f.character.kitDefinition.presentation.entries.Single(e => e.entryId == "hold").minimumPotentialLevel = 3;
            f.character.kitDefinition.presentation.entries.Single(e => e.entryId == "hold").unlockTrack = PotentialTrack.Skill;
            Require(!f.Build().Sections.SelectMany(s => s.Entries).Any(e => e.Id == "hold"), "Explicit effect-granted unlock also applies");
            f.progression.SetPotentialLevel(f.character, PotentialTrack.Skill, 3);
            Require(f.Entry("hold").HeaderTags.Count > 0, "Entry remains learned without its transient ready tag");
        });
        Check(report, "Root levels cover zero through four without repeating on children", () => {
            using var f = new Fixture();
            for (int level = 0; level <= 4; level++)
            {
                foreach (PotentialTrack track in Enum.GetValues(typeof(PotentialTrack))) f.progression.SetPotentialLevel(f.character, track, level);
                var model = f.Build();
                foreach (var section in model.Sections.Where(s => s.Id == "skill" || s.Id == "ultimate" || s.Id == "passive"))
                    Require(section.LevelLabel == "Lv." + level, "Current level at section root");
                Require(model.Sections.SelectMany(s => s.Entries).All(e => !e.Metadata.Contains("Potential")), "No repeated child level labels");
            }
        });
        Check(report, "Normal Attack is excluded even when explicitly authored while its QTE reuse remains", () => {
            using var f = new Fixture();
            var normal = new ComboAction { actionId = "normal", actionName = "Normal" };
            f.table.comboActions.Add(normal);
            f.character.qteSwap.enabled = true; f.character.qteSwap.introActionKey = "normal";
            f.character.kitDefinition.presentation = CharacterKitInfoBuilder.CreateLegacyPresentation(f.character);
            f.character.kitDefinition.presentation.sections.Add(new CharacterKitPresentationDefinition.Section { sectionId = "basic" });
            f.character.kitDefinition.presentation.entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = "ordinary", sectionId = "basic", actionId = "normal" });
            Require(f.Build().Sections.All(s => s.Id != "basic"), "No Basic section");
            Require(f.Entry("qte.normal").ActionId == "normal", "QTE references keep their own role");
        });
        Check(report, "Ground Airborne Both and cooldown live in structured header tags", () => {
            using var f = new Fixture();
            foreach (CombatActionContext context in Enum.GetValues(typeof(CombatActionContext)))
            {
                f.tap.context = context;
                var entry = f.Entry("tap");
                Require(entry.HeaderTags.Count == 3, "Context, cooldown and energy tags");
                Require(entry.HeaderTags[0].Kind == (context == CombatActionContext.Ground ? CharacterKitViewModel.TagKind.Ground : context == CombatActionContext.Air ? CharacterKitViewModel.TagKind.Airborne : CharacterKitViewModel.TagKind.Both), "Correct context tag");
                Require(!entry.Metadata.Contains("Cooldown") && !entry.Description.Contains("Grounded") && !entry.Description.Contains("Airborne"), "Compact header owns generated context/cooldown");
            }
        });
        Check(report, "All authored characters at every Potential tier", ValidateAssets);
        Check(report, "Nested cards render at wide and 4:3 sizes without clipped text", ValidatePanel);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/native-checks.json", JsonUtility.ToJson(report, true));
        Debug.Log($"Kit information checks: {report.passed.Count} passed, {report.failed.Count} failed.");
    }

    private static void RuntimeParity()
    {
        using var f = new Fixture();
        var node = f.character.kitDefinition.basePassiveNode;
        node.cooldownModifiers.Add(new PassiveCooldownModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", operation = PassiveNumericOperation.Multiply, value = .5f });
        node.actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Ultimate, actionId = "tap", damageMultiplierOperation = PassiveValueModifierOperation.Add, damageMultiplierValue = 2,
            cooldownOperation = PassiveValueModifierOperation.Add, cooldownValue = 1, energyCostOperation = PassiveValueModifierOperation.Override, energyCostValue = -1 });
        var owner = new GameObject("Kit runtime parity"); owner.SetActive(false);
        try
        {
            var player = owner.AddComponent<PlayerCharacter>(); player.characterObject = f.character;
            var runtime = owner.AddComponent<CharacterPassiveController>(); runtime.playerCharacter = player;
            Require(Mathf.Approximately(runtime.ModifyCooldown(AttackCategory.Ultimate, "tap", "Tap", 10), 6), "Cooldown ordering");
            Require(Mathf.Approximately(runtime.ModifyActionDamageMultiplier(AttackCategory.Ultimate, "tap", "Tap", 5), 7), "Damage operation");
            Require(runtime.ModifyUltimateEnergyCost("tap", "Tap", 60) == 0, "Energy clamp");
            Require(Mathf.Approximately(runtime.ModifyCooldown(AttackCategory.Normal, "absent", "Absent", -4), .05f), "Existing minimum cooldown");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private static void ValidateAssets()
    {
        var progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterObject", new[] { "Assets/Project/Character/Hero" }))
            {
                var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(AssetDatabase.GUIDToAssetPath(guid));
                bool main = character.characterId == "char-0-main";
                bool memory = character.kitDefinition.useMemoryRanks;
                int maxLevel = memory ? CharacterMemoryProgress.UpgradesPerFamily : CharacterKitDefinition.MaxPotentialLevel;
                for (int level = 0; level <= maxLevel; level++)
                {
                    if (memory) CharacterMemoryTests.SetMemoryRanks(progression, character, level, level, level);
                    else foreach (PotentialTrack track in Enum.GetValues(typeof(PotentialTrack))) progression.SetPotentialLevel(character, track, level);
                    var model = CharacterKitInfoBuilder.Build(character, new KitInfoContext(progression));
                    Require(model.Diagnostics.Count == 0, character.name + ": " + string.Join("; ", model.Diagnostics));
                    if (character.characterId == "Char_2_Rugal")
                        Require(model.Sections.SelectMany(s => s.Entries).Any(e => e.ActionId == "rugal.skill.ex") == (level == 4), "Omega follow-up learns at Passive 4");
                    if (character.characterId == "char-1-camera_girl")
                        Require(model.Sections.SelectMany(s => s.Entries).Any(e => e.ActionId == "cg.skill.ex") == (level >= 1), "Shadow Assist learns at Ultimate 1");
                    if (main)
                    {
                        Require(model.Sections.Single(s => s.Id == "ultimate").Entries.Count == (level >= 3 ? 2 : 1), "Execution Breakdown unlocks at Passive Memory 3");
                        Require(model.Sections.All(s => s.Id != "basic"), "Normal Attack is excluded");
                        Require(model.Sections.Single(s => s.Id == "skill").LevelLabel == "Lv." + level, "Potential level belongs to root");
                        Require(model.Sections.SelectMany(s => s.Entries).Any(e => e.ActionId == "main.skill.dimension_slash") == (level == 4), "Dimension Slash is shown only at Passive 4");
                        Require(model.Sections.Single(s => s.Id == "qte").Entries.Single().ActionId == "main.qte.lightning_plunge", "Main QTE");
                        Require(model.Sections.Single(s => s.Id == "special").Entries.Any(e => e.ActionId == "main.dash.dimension_cut"), "Main standalone");
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(progression); }
    }

    private static void ValidatePanel()
    {
        var root = PrefabUtility.LoadPrefabContents(CharacterInspectorFolderAuthoring.CanvasPath);
        var inventory = ScriptableObject.CreateInstance<Project.ShopSystem.PlayerRunInventory>();
        var item = ScriptableObject.CreateInstance<CharacterItem>();
        var progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
        try
        {
            item.characterObjectData = AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
            inventory.ownedItems.Clear(); inventory.ownedItems.Add(item);
            var inspector = root.GetComponentInChildren<InspectScreenUI>(true);
            inspector.GetType().GetField("playerInventory", Private).SetValue(inspector, inventory);
            inspector.GetType().GetField("zoneStateProvider", Private).SetValue(inspector, null);
            var teamUI = root.GetComponent<Project.UI.TeamUI>(); if (teamUI != null) Object.DestroyImmediate(teamUI);
            root.GetComponentInChildren<TeamBuilderUI>(true).gameObject.SetActive(false);
            inspector.GetType().GetMethod("Awake", Private).Invoke(inspector, null);
            inspector.Open(item);
            inspector.ShowSkillPage();
            var panel = root.GetComponentInChildren<CharacterSkillPanel>(true);
            panel.PopulateData(item, null);
            var bind = panel.GetType().GetMethod("BindModel", Private);
            bind.Invoke(panel, new object[] { CharacterKitInfoBuilder.Build(item.characterObjectData, new KitInfoContext(progression)) });
            var content = (Transform)panel.GetType().GetField("entryContainer", Private).GetValue(panel);
            Require(content.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("KitSection_")) == 5, "Five sections without Normal Attack");
            Directory.CreateDirectory(Output);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
            {
                string name = "kit-main-" + size.x;
                Capture(root, name, size.x, size.y, 1);
                foreach (var text in content.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    if (!text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) continue;
                    Require(text.rectTransform.rect.width > 0, "Positive text width: " + text.name);
                    if (text.name == "Description" || text.name == "Metadata" || text.name == "Title")
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "Text height fits: " + text.name);
                    if (text.name == "TagLabel")
                        Require(text.preferredWidth <= text.rectTransform.rect.width + 1, "Header tag fits: " + text.text);
                }
            }
            File.Copy(Output + "/kit-main-1280.png", Output + "/kit-main-lv0.png", true);
            Require(content.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.name == "SectionLevel" && t.text == "Lv.0"), "Level displayed at root");
            Require(content.GetComponentsInChildren<KitHeaderTagGraphic>().Length > 0, "Header symbols rendered");
            foreach (PotentialTrack track in Enum.GetValues(typeof(PotentialTrack))) progression.SetPotentialLevel(item.characterObjectData, track, 4);
            bind.Invoke(panel, new object[] { CharacterKitInfoBuilder.Build(item.characterObjectData, new KitInfoContext(progression)) });
            Capture(root, "kit-main-lv4", 1280, 720, 1);
            var scroll = panel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
            {
                Capture(root, "kit-main-middle", 1280, 720, .5f);
                Capture(root, "kit-main-bottom", 1280, 720, 0);
            }
            panel.PopulateData(null, null);
            Require(content.Cast<Transform>().All(t => !t.gameObject.activeSelf), "Clearing selection hides stale cards");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); Object.DestroyImmediate(item); Object.DestroyImmediate(inventory); Object.DestroyImmediate(progression); }
    }

    private static void Capture(GameObject root, string name, int width, int height, float position)
    {
        var canvas = root.GetComponent<Canvas>(); root.transform.localScale = Vector3.one;
        foreach (var group in root.GetComponents<CanvasGroup>()) group.alpha = 1;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("Kit information preview camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, root.scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 5.4f; camera.scene = root.scene; camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .04f, .07f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active; var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var scroll = root.GetComponentInChildren<CharacterSkillPanel>(true).GetComponentInChildren<ScrollRect>(true);
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var layout in root.GetComponentsInChildren<VerticalLayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                scroll.verticalNormalizedPosition = position;
            }
            Canvas.ForceUpdateCanvases();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    private static void Check(Report report, string name, Action run)
    {
        try { run(); report.passed.Add(name); }
        catch (Exception error) { report.failed.Add(name + ": " + error); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
