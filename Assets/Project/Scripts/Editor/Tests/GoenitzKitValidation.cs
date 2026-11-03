using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GoenitzKitValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private sealed class Report
    {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }

    private sealed class Fixture : IDisposable
    {
        public readonly CharacterObject owner;
        public readonly CharacterObject ally;
        public readonly CharacterProgressionState progression;
        public readonly CharacterInventoryState inventory;
        public readonly TeamLoader team;
        public readonly GoenitzKitRuntimeDefinition data;
        public readonly CharacterKitRuntimeHost host;
        public readonly ICharacterKitRuntime runtime;
        public readonly CharacterPassiveController passive;
        public readonly PlayerCharacter player;
        private readonly TeamLoader _previous;
        private readonly GameObject _object;
        private readonly List<GameObject> _enemies = new List<GameObject>();
        private readonly List<CharacterItem> _items = new List<CharacterItem>();

        public Fixture(int ultimate = 4, int passives = 4, int skill = 0)
        {
            owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(GoenitzKitAuthoring.Folder + "CharacterObject_Goenitz.asset"));
            owner.characterId = "validation-goenitz";
            owner.stats.attack = 100f; owner.upgrade.attackPerLevel = 0f;
            ally = ScriptableObject.CreateInstance<CharacterObject>(); ally.characterId = "validation-ally";
            ally.stats.attack = 200f; ally.stats.maxHP = 1000f;
            progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
            progression.SetPotentialLevel(owner, PotentialTrack.Ultimate, ultimate);
            progression.SetPotentialLevel(owner, PotentialTrack.Passive, passives);
            progression.SetPotentialLevel(owner, PotentialTrack.Skill, skill);
            inventory = ScriptableObject.CreateInstance<CharacterInventoryState>();
            foreach (var character in new[] { owner, ally })
            {
                var item = ScriptableObject.CreateInstance<CharacterItem>();
                item.characterObjectData = character; item.id = character.characterId;
                _items.Add(item); inventory.AddItem(item);
            }
            inventory.DeployTeam(new[] { owner, ally }, 0);
            inventory.SetRunCharacterCurrentHP(owner, 1000f); inventory.SetRunCharacterCurrentHP(ally, 1000f);
            _object = new GameObject("Goenitz validation fixture"); _object.SetActive(false);
            team = _object.AddComponent<TeamLoader>();
            _previous = TeamLoader.Instance;
            SetSingleton(team);
            Set(team, "runInventory", inventory); Set(team, "metaProgression", progression);
            Set(team, "teamSnapshot", inventory.GetTeamSnapshot());
            Invoke(team, "BindRunInventoryEvents"); Invoke(team, "BindMetaProgressionEvents");
            host = new CharacterKitRuntimeHost(team); Set(team, "_kitRuntimes", host);
            host.Reconcile(inventory.GetTeamSnapshot());
            data = owner.kitDefinition.runtimeDefinition as GoenitzKitRuntimeDefinition;
            runtime = host.Find(owner);
            player = _object.AddComponent<PlayerCharacter>(); player.characterObject = owner;
            passive = _object.AddComponent<CharacterPassiveController>(); passive.playerCharacter = player;
            player.PassiveController = passive; passive.SetMetaProgression(progression); passive.InitializeFromCharacterData();
            Require(runtime != null && data != null, "Authored runtime must be deployed.");
        }

        public UltimateAction Ultimate(string id) => owner.actionResolverTable.ultimateActions.Single(a => a.actionId == id);
        public CombatActionExecutionToken Cast(string id)
        {
            var action = Ultimate(id);
            var cast = new CombatActionExecutionToken(owner, action);
            runtime.OnCastAccepted(cast, action);
            return cast;
        }
        public void Enter(float ae = 70f)
        {
            team.SetCharacterCurrentAE(owner, ae);
            CombatActionExecutionToken cast = Cast(data.transformationActionId);
            runtime.OnActionActive(cast, cast.action);
        }
        public void Tick(float delta) { host.Tick(delta); inventory.TickRuntimeEffects(delta); }
        public RuntimeEffectInstance Effect(CharacterObject actor, RuntimeEffectRecipe recipe)
            => inventory.GetActiveRuntimeEffects(actor).FirstOrDefault(e => e.recipe == recipe);
        public EnemyController Enemy(bool dead = false)
        {
            var go = new GameObject("Goenitz validation enemy"); _enemies.Add(go);
            go.transform.position = new Vector3(30000f, 30000f, 0f);
            var enemy = go.AddComponent<EnemyController>();
            Invoke(enemy, "Awake"); Invoke(enemy, "Start"); Invoke(enemy, "TransitionToActiveLifecycle");
            enemy.maxHP = enemy.currentHP = 10000f; enemy.defense = enemy.damageRes = 0f;
            if (dead) Set(enemy, "isAlive", false);
            return enemy;
        }
        public void Kill(CharacterObject actor, EnemyController enemy, DamageDelivery delivery = DamageDelivery.Primary)
        {
            runtime.OnDamageResolved(new DamageResolvedEvent(default, new DamageDescriptor(delivery, ElementType.None),
                DamageProcPolicy.PublishResolvedEvent | DamageProcPolicy.PublishKillEvent,
                actor, null, enemy, AttackCategory.Normal, "validation.kill", 10, 10, false, true, Vector2.zero));
        }
        public DamageRequest Request(CharacterObject actor, EnemyController enemy, string actionId = "validation.hit", CombatActionExecutionToken cast = null)
            => new DamageRequest
            {
                sourceCharacter = actor, sourceStats = team.GetCharacterRuntimeStats(actor), target = enemy,
                actionId = actionId, category = AttackCategory.Normal, baseDamage = 100f,
                descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None, DamageCriticalPolicy.Prevent),
                execution = new CombatExecutionContext(null, actor, 0, false, cast?.action, cast)
            };
        public void Dispose()
        {
            host.Dispose(); Set(team, "_kitRuntimes", null);
            foreach (var go in _enemies) Object.DestroyImmediate(go);
            Object.DestroyImmediate(_object);
            SetSingleton(_previous);
            foreach (var item in _items) Object.DestroyImmediate(item);
            Object.DestroyImmediate(inventory); Object.DestroyImmediate(progression); Object.DestroyImmediate(owner); Object.DestroyImmediate(ally);
        }
    }

    [MenuItem("Tools/Project/Validation/Run Goenitz Kit Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run isolated checks outside Play Mode.");
        var report = new Report();
        Check(report, "Hold Skill is level gated and separates press from hold", () =>
        {
            using var f = new Fixture(skill: 1);
            var ctx = new InputContext { isGrounded = true, passiveController = f.passive, runtimeState = f.passive.RuntimeState };
            var resolver = new ActionResolver(); resolver.ResolveInitial(ctx, f.owner.actionResolverTable, AttackCategory.Skill);
            Require(resolver.ActiveCombatAction.actionId == "goenitz.skill.yonokaze", "Press must select normal Skill.");
            ctx.skillInputHeld = true; resolver.ResolveInitial(ctx, f.owner.actionResolverTable, AttackCategory.Skill);
            Require(resolver.ActiveCombatAction.actionId == f.data.exSkillActionId, "Hold must select unlocked EX.");
            f.progression.SetPotentialLevel(f.owner, PotentialTrack.Skill, 0);
            Require(resolver.ResolveInitial(ctx, f.owner.actionResolverTable, AttackCategory.Skill) == null, "Locked EX cannot resolve.");
        });
        Check(report, "Held Skill buffer lasts until release/consume and clears with other inputs", () =>
        {
            var buffer = new ActionInputBuffer(); buffer.BufferSkill(held: true); buffer.SustainSkillHold();
            Require(buffer.SkillInputHeld, "Hold intent must persist."); buffer.ConsumeSkill();
            Require(!buffer.HasSkillBuffer && !buffer.SkillInputHeld, "Consumption must clear sustained hold.");
            buffer.BufferSkill(held: true); buffer.SustainSkillHold(); buffer.BufferAttack(); buffer.ConsumeAttack();
            Require(!buffer.HasSkillBuffer, "Other accepted input must clear held Skill.");
        });
        Check(report, "Discrete drain is frame independent and zero delta does not tick", () =>
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var f = new Fixture(); f.Enter();
                for (int i = 0; i < fps * 6; i++) f.Tick(1f / fps);
                Near(f.team.GetCharacterCurrentAE(f.owner), 52f, "Six seconds at " + fps + " FPS");
                float before = f.team.GetCharacterCurrentAE(f.owner); f.Tick(0f);
                Near(f.team.GetCharacterCurrentAE(f.owner), before, "Zero delta");
            }
        });
        Check(report, "AE drain catches up off-field and expires shared state", () =>
        {
            using var f = new Fixture(); f.Enter();
            f.Tick(23f); Near(f.team.GetCharacterCurrentAE(f.owner), 1f, "70 AE at tick 23");
            Require(f.runtime.Affects(f.ally), "Ally must share owner duration.");
            f.Tick(1f); Near(f.team.GetCharacterCurrentAE(f.owner), 0f, "Final partial drain");
            Require(!f.runtime.Affects(f.owner) && !f.runtime.Affects(f.ally), "No independent ally timer.");
            Near(f.inventory.GetEvasionChanceAdd(f.ally), 0f, "Evasion must end with mode.");
        });
        Check(report, "Ordinary gains are blocked but costs and classified extension work", () =>
        {
            using var f = new Fixture(); f.Enter(30f);
            f.team.AddCharacterAE(f.owner, 5f); Near(f.team.GetCharacterCurrentAE(f.owner), 30f, "Ordinary reward blocked");
            f.team.AddCharacterAE(f.owner, -3f); Near(f.team.GetCharacterCurrentAE(f.owner), 27f, "Negative spend remains valid");
            f.team.AddCharacterAE(f.owner, 6f, AuraEnergyChangeReason.ModeKillExtension);
            Near(f.team.GetCharacterCurrentAE(f.owner), 33f, "Classified extension");
            f.team.SetCharacterCurrentAE(f.owner, 0f); f.team.AddCharacterAE(f.owner, 5f);
            Near(f.team.GetCharacterCurrentAE(f.owner), 5f, "Normal gains restored after exit");
        });
        Check(report, "Blizzard refreshes one stack and outlasts Frenzy", () =>
        {
            using var f = new Fixture(); f.Enter();
            RuntimeEffectInstance first = f.Effect(f.owner, f.data.blizzard);
            Near(first.remainingSeconds, 15f, "Blizzard duration");
            f.Tick(5f); f.Cast(f.data.finisherActionId);
            Require(!f.runtime.Affects(f.owner), "Shin consumes mode.");
            Near(f.Effect(f.owner, f.data.blizzard).remainingSeconds, 15f, "Recast refresh");
            Require(f.inventory.GetRuntimeEffectStackCount(f.owner, f.data.blizzard.recipeId) == 1, "Blizzard cannot stack.");
            f.inventory.RemoveRuntimeEffectsOnOwnerDamaged(f.owner);
            Require(f.Effect(f.owner, f.data.blizzard) != null, "Damage must not remove Blizzard.");
            f.Tick(15.01f); Require(f.Effect(f.owner, f.data.blizzard) == null, "Independent timer expires.");
            Require(f.data.blizzard.parameters.statModifiers.Any(m => m.target == RuntimeEffectStatTarget.AdditionalDamageIncrease && m.value == 10f), "Blizzard includes +10 ADI.");
        });
        Check(report, "Any Goenitz kill grants ATK, cap 10 still extends; allies never reward", () =>
        {
            using var f = new Fixture(); f.Enter(30f);
            for (int i = 0; i < 12; i++) f.Kill(f.owner, f.Enemy(true), i % 2 == 0 ? DamageDelivery.Dot : DamageDelivery.Additional);
            Require(f.Effect(f.owner, f.data.killAttack).stackCount == 10, "Cap is ten.");
            f.team.SetCharacterCurrentAE(f.owner, 20f);
            f.Kill(f.ally, f.Enemy(true)); Near(f.team.GetCharacterCurrentAE(f.owner), 20f, "Ally kill ignored");
            f.Kill(f.owner, f.Enemy(true)); Near(f.team.GetCharacterCurrentAE(f.owner), 26f, "Kill at cap still extends");
            f.team.SetCharacterCurrentAE(f.owner, 0f);
            Require(f.Effect(f.owner, f.data.killAttack).stackCount == 10, "Mode exit cannot remove stacks.");
            f.Tick(16f); Near(f.team.GetCharacterRuntimeStats(f.owner).attack, 130f, "Ten stacks add 30% clean-base ATK after Blizzard expires");
            f.inventory.RemoveRuntimeEffectsOnOwnerDamaged(f.owner);
            Require(f.Effect(f.owner, f.data.killAttack) == null, "Actual damage clears ATK stacks.");
        });
        Check(report, "Kill events deduplicate by enemy life", () =>
        {
            using var f = new Fixture(); f.Enter(30f); var enemy = f.Enemy(true);
            f.Kill(f.owner, enemy); f.Kill(f.owner, enemy, DamageDelivery.Additional);
            Near(f.team.GetCharacterCurrentAE(f.owner), 36f, "One victim is one extension");
            Require(f.Effect(f.owner, f.data.killAttack).stackCount == 1, "One victim is one stack.");
        });
        Check(report, "Remembered first lethal hit resumes mode after full Ultimate cost", () =>
        {
            using var f = new Fixture(); f.Enter(); var cast = f.Cast(f.data.ultimateActionId);
            f.team.AddCharacterAE(f.owner, -70f, AuraEnergyChangeReason.ActionCost);
            Require(!f.runtime.Affects(f.owner), "Full spend ends mode.");
            var request = f.Request(f.owner, f.Enemy(true), f.data.ultimateActionId, cast);
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(100f, false, request.descriptor), 100f);
            Near(f.team.GetCharacterCurrentAE(f.owner), 6f, "Remembered kill restores precisely 6 AE");
            Require(f.runtime.Affects(f.ally) && !cast.modeEligibilityPending, "Restore sharing and consume memory.");
            f.team.SetCharacterCurrentAE(f.owner, 0f);
            request.target = f.Enemy(true);
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(100f, false, request.descriptor), 100f);
            Near(f.team.GetCharacterCurrentAE(f.owner), 0f, "Memory cannot be reused");
        });
        Check(report, "Remembered nonlethal hit cannot reward a later hit after exit", () =>
        {
            using var f = new Fixture(); f.Enter(); var cast = f.Cast(f.data.finisherActionId);
            var request = f.Request(f.owner, f.Enemy(), f.data.finisherActionId, cast);
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(1f, false, request.descriptor), 1f);
            Require(!cast.modeEligibilityPending, "Nonlethal hit consumes remembered eligibility.");
            f.team.SetCharacterCurrentAE(f.owner, 0f); request.target = f.Enemy(true);
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(1f, false, request.descriptor), 1f);
            Near(f.team.GetCharacterCurrentAE(f.owner), 0f, "Later hit cannot resurrect mode");
        });
        Check(report, "Shared primary hit applies one vulnerability and actor-owned Bleed plus native DOT", () =>
        {
            using var f = new Fixture(ultimate: 2); f.Enter();
            var enemy = f.Enemy(); float bleedBasis = 0f; CharacterObject actor = null; int primary = 0, additional = 0;
            Action<DamageResolvedEvent> listener = e =>
            {
                if (e.target != enemy) return;
                if (e.descriptor.delivery == DamageDelivery.Primary) primary++;
                if (e.descriptor.delivery == DamageDelivery.Additional) { additional++; bleedBasis = e.baseDamage; actor = e.sourceCharacter; }
            };
            CombatEventHub.Shared.DamageResolved += listener;
            try { CombatDamageProcessor.Apply(f.Request(f.ally, enemy)); }
            finally { CombatEventHub.Shared.DamageResolved -= listener; }
            Require(primary == 1 && additional == 1 && actor == f.ally, "One primary, one terminal actor-owned Bleed packet.");
            Near(bleedBasis, 100f, "50% of ally's 200 ATK, not Goenitz ATK");
            Require(enemy.DamageTypeState.HasActiveDot(ElementType.Bleed), "Additional Bleed explicitly applies native DOT.");
            Require(enemy.RuntimeEffects.GetStackCount(f.data.vulnerability.recipeId) == 1, "One debuff from ally hit.");
            CombatDamageProcessor.Apply(f.Request(f.owner, enemy));
            Require(enemy.RuntimeEffects.GetStackCount(f.data.vulnerability.recipeId) == 1, "Self and ally refresh the same debuff.");
        });
        Check(report, "Physical shred changes Physical resistance rather than all damage taken", () =>
        {
            using var f = new Fixture(ultimate: 0, passives: 1); var enemy = f.Enemy();
            var request = f.Request(f.owner, enemy);
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(10f, true, request.descriptor), 10f);
            Near(enemy.DamageTypeState.ModifyIncomingDamage(ElementType.None, 100f), 115f, "Physical reduction -15");
            Near(enemy.DamageTypeState.ModifyIncomingDamage(ElementType.Bleed, 100f), 100f, "Bleed resistance unchanged");
            f.runtime.OnPositivePrimaryHit(request, new DamageResult(10f, true, request.descriptor), 10f);
            Near(enemy.DamageTypeState.ModifyIncomingDamage(ElementType.None, 100f), 115f, "Shred does not stack");
        });
        Check(report, "Frenzy-gated post-dash entry resolves actual Attack4 and preserves mode", () =>
        {
            using var f = new Fixture(passives: 2); f.Enter();
            f.passive.NotifyAfterDash();
            var ctx = new InputContext { isGrounded = true, passiveController = f.passive, runtimeState = f.passive.RuntimeState };
            var resolver = new ActionResolver(); var step = resolver.ResolveInitial(ctx, f.owner.actionResolverTable, AttackCategory.Normal);
            Require(step.stepName == "Attack4", "Conditional root route must not shift stage-4 entry.");
            f.passive.ClearActionAvailabilityWindow(AttackCategory.Normal, resolver.ActiveCombatAction.actionId, resolver.ActiveCombatAction.actionName);
            Require(f.passive.RuntimeState.HasTag(f.data.modeTag), "Persistent mode is not a consumed action window.");
        });
        Check(report, "Attack2-2 follows Attack1 only while Frenzy is active", () =>
        {
            using var f = new Fixture(passives: 2);
            var ctx = new InputContext { isGrounded = true, passiveController = f.passive, runtimeState = f.passive.RuntimeState };
            var resolver = new ActionResolver();
            Require(resolver.ResolveInitial(ctx, f.owner.actionResolverTable).stepName == "Attack1", "Normal opener");
            Require(resolver.ResolveNext(ctx).stepName == "Attack2-1", "Normal follow-up");
            f.Enter();
            Require(resolver.ResolveInitial(ctx, f.owner.actionResolverTable).stepName == "Attack1", "Frenzy opener");
            Require(resolver.ResolveNext(ctx).stepName == "Attack2-2", "Frenzy follow-up");
        });
        Check(report, "Cooldown display selects the latest action with an active cooldown", () =>
        {
            using var f = new Fixture(skill: 1);
            var combat = f.player.gameObject.AddComponent<PlayerCombat>();
            combat.playerCharacter = f.player;
            var normal = f.owner.actionResolverTable.skillActions.First();
            var ex = f.owner.actionResolverTable.skillActions.Single(a => a.actionId == f.data.exSkillActionId);
            combat.StartActionCooldown(AttackCategory.Skill, normal.actionName, normal.actionId);
            combat.StartActionCooldown(AttackCategory.Skill, ex.actionName, ex.actionId);
            Require(combat.GetLatestCoolingAction(f.owner, AttackCategory.Skill) == ex, "EX supersedes earlier normal cooldown");
            combat.ResetActionCooldown(AttackCategory.Skill, ex.actionName, ex.actionId);
            Require(combat.GetLatestCoolingAction(f.owner, AttackCategory.Skill) == normal, "Earlier live cooldown resumes display");
        });
        Check(report, "Tornado hit budgets and Ultimate portions match authored totals", ValidateBudgets);
        const string folder = "Docs_JJK/3_Outputs/Reports/GoenitzKit";
        Directory.CreateDirectory(folder); File.WriteAllText(folder + "/validation.json", JsonUtility.ToJson(report, true));
        Debug.Log($"Goenitz kit validation: {report.passed.Count} passed, {report.failed.Count} failed. {folder}/validation.json");
        if (report.failed.Count > 0) Debug.LogError(string.Join("\n", report.failed));
    }

    private static void ValidateBudgets()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(GoenitzKitAuthoring.Folder + "CharacterObject_Goenitz.asset");
        foreach (string name in new[] { "skill", "skill_lv4", "ex_skill", "ex_skill_lv4" })
        {
            var p = AssetDatabase.LoadAssetAtPath<ProjectileData>(GoenitzKitAuthoring.Folder + "ProjectileData_goe_" + name + ".asset");
            Near(((NormalHitbox)p.ResolveRoutineHitbox()).damageRatio * p.maxHits, 1f, name + " total hit fraction");
            Require(p.hitIntervalSeconds > 0f, name + " uses seconds cadence.");
            if (name.StartsWith("ex_"))
                Require(p.traversalMode == ProjectileTraversalMode.GroundHoming, name + " tracks along the launch plane.");
        }
        foreach (UltimateAction action in character.actionResolverTable.ultimateActions.Where(a => a.energyCost > 0 || a.actionId.Contains("shin")))
        {
            float weight = 0f;
            foreach (ComboNode node in GoenitzKitAuthoring.Nodes(new[] { action.comboTree.rootNode }))
                if (node.stepData?.hitData != null)
                    foreach (var hit in node.stepData.hitData.hitboxes)
                    {
                        if (hit is NormalHitbox normal) weight += normal.damageRatio;
                        if (hit is ProjectileSpawnHitbox spawn && spawn.projectileData.ResolveRoutineHitbox() is NormalHitbox box)
                            weight += box.damageRatio * spawn.projectileData.maxHits;
                    }
            Near(weight, 1f, action.actionName + " normalized action total");
        }
        Require(character.actionResolverTable.qteActions.Any(a => a.actionId == "goenitz.qte"), "Existing QTE preserved.");
    }
    private static void Check(Report report, string label, Action check)
    {
        try { check(); report.passed.Add(label); }
        catch (Exception e) { report.failed.Add(label + ": " + (e.InnerException ?? e).Message); }
    }
    private static void Near(float actual, float expected, string label)
        => Require(Mathf.Abs(actual - expected) < .01f, label + $": expected {expected}, got {actual}");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void SetSingleton(TeamLoader team) => typeof(TeamLoader).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, team);
}
