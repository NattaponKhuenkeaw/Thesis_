#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Combat;
using Project.ShopSystem;
using Project.InventorySystem.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class MainSummonPlayTests
{
    [UnityTest]
    public IEnumerator AuthoredSummonKeepsSourceStatsAndCadenceAfterSwitchAndStopsOnRoomClear()
    {
        yield return new EnterPlayMode();
        VerifySummonLifecycle();
        yield return new ExitPlayMode();
    }

    [Test]
    public void TeamInventorySummonSurvivesSetCharacterAndKeepsOneTimer()
    {
        VerifySummonLifecycle();
    }

    private static void VerifySummonLifecycle()
    {
        var main = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset"));
        var ally = ScriptableObject.CreateInstance<CharacterObject>();
        var host = new GameObject("Summon test owner");
        host.SetActive(false);
        var managerHost = new GameObject("Summon test pool");
        managerHost.SetActive(false);
        var previousPool = ProjectileVFXManager.Instance;
        var previousManager = ProjectileManager.Instance;
        var previousPlayer = PlayerCharacter.Instance;
        var previousTeam = TeamLoader.Instance;
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var mainItem = ScriptableObject.CreateInstance<CharacterItem>();
        var allyItem = ScriptableObject.CreateInstance<CharacterItem>();
        var teamHost = new GameObject("Summon test team");
        teamHost.SetActive(false);
        var targetHost = new GameObject("Summon nearest enemy");
        var distantHost = new GameObject("Summon distant enemy");
        try
        {
            main.characterId = "summon-test-main";
            ally.characterId = "summon-test-ally";
            main.stats = new CharacterStats { maxHP = 1000f, attack = 100f, critRate = 0f };
            ally.stats = new CharacterStats { maxHP = 9000f, attack = 9000f };
            var player = host.AddComponent<PlayerCharacter>();
            var combat = host.AddComponent<PlayerCombat>();
            player.characterObject = main;
            player.Combat = combat;
            combat.playerCharacter = player;
            player.InitializeHP(1000f);
            SetInstance(typeof(PlayerCharacter), player);
            mainItem.id = "summon-test-main-item";
            mainItem.characterObjectData = main;
            allyItem.id = "summon-test-ally-item";
            allyItem.characterObjectData = ally;
            inventory.ownedItems.Clear();
            inventory.ownedItems.Add(mainItem);
            inventory.ownedItems.Add(allyItem);
            inventory.DeployTeam(new[] { main, ally }, 0);
            var team = teamHost.AddComponent<TeamLoader>();
            team.playerCharacter = player;
            typeof(TeamLoader).GetField("runInventory", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(team, inventory);
            SetInstance(typeof(TeamLoader), team);
            team.RefreshRuntimeStatsAndResources();
            host.transform.position = Vector3.right * 3000f;
            var pool = managerHost.AddComponent<ProjectileVFXManager>();
            var manager = managerHost.AddComponent<ProjectileManager>();
            SetInstance(typeof(ProjectileVFXManager), pool);
            SetInstance(typeof(ProjectileManager), manager);
            managerHost.SetActive(true);
            var target = EnemyLifecycleTestUtility.AddActive(targetHost);
            var distant = EnemyLifecycleTestUtility.AddActive(distantHost);
            target.maxHP = target.currentHP = 10000f;
            distant.maxHP = distant.currentHP = 10000f;
            target.defense = target.damageRes = 0f;
            distant.defense = distant.damageRes = 0f;
            targetHost.transform.position = Vector3.right * 3002f;
            distantHost.transform.position = Vector3.right * 3007f;
            // The isolated EditMode case checks the shared timer with no enemies.
            // The PlayMode wrapper also exercises the actual projectile pool and hit dispatch.
            if (Application.isPlaying)
            {
                RoomEnemyRegistry.Register(target);
                RoomEnemyRegistry.Register(distant);
            }
            else
            {
                RoomEnemyRegistry.Unregister(target);
                RoomEnemyRegistry.Unregister(distant);
            }
            var skillNode = main.kitDefinition.GetNodeById("main.skill.lv4");
            var recipe = skillNode.runtimeEffects.Single(effect => effect.recipe?.recipeId == "main.memory.chain_summon").recipe;
            var targeting = recipe.parameters.projectile;
            Assert.That(targeting.useActivePlayerOrigin, Is.True);
            Vector2 castPosition = host.transform.position;
            var strikeData = AssetDatabase.LoadAssetAtPath<ProjectileData>(
                "Assets/Project/Character/Hero/test_0_Main/ProjectileData_main_chain_strike.asset");
            var damageApplication = strikeData.onHitEffects.Single(application => application.triggerOnHit);
            Assert.That(inventory.ApplyRuntimeEffect(main, recipe, main, main.stats), Is.True);
            var instance = inventory.GetActiveRuntimeEffects(main).Single(e => e.recipe.recipeId == recipe.recipeId);
            inventory.TickRuntimeEffects(2.49f);
            Assert.That(managerHost.GetComponentsInChildren<ProjectileRuntime>().Length, Is.Zero);
            // The current player supplies the target search origin while Main keeps damage ownership.
            inventory.SetActiveTeamSlotIndex(1);
            player.SetCharacter(ally, 9000f, ally.stats);
            host.transform.position = Vector3.right * 20f;
            main.stats.maxHP = 2000f;
            team.RefreshRuntimeStatsAndResources();
            var originResolver = typeof(PeriodicProjectileRuntimeEffectDefinition).GetMethod("ResolveStrikeOrigin",
                BindingFlags.NonPublic | BindingFlags.Static);
            var origin = (Vector2)originResolver.Invoke(null, new object[] { targeting, castPosition });
            Assert.That(origin, Is.EqualTo((Vector2)host.transform.position));
            inventory.TickRuntimeEffects(.01f);
            Assert.That(inventory.GetActiveRuntimeEffects(main).Single(), Is.SameAs(instance));
            Assert.That(instance.elapsedSeconds, Is.EqualTo(2.5f).Within(.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(ally), Is.Empty);
            Assert.That(instance.formulaState, Is.Not.Null);
            Assert.That(instance.remainingSeconds, Is.EqualTo(12.5f).Within(.001f));
            if (Application.isPlaying)
            {
                var strike = managerHost.GetComponentsInChildren<ProjectileRuntime>().Single();
                var spawn = (ProjectileSpawnContext)typeof(ProjectileRuntime).GetField("spawnContext", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(strike);
                Assert.That(spawn.origin, Is.EqualTo(origin));
                Assert.That(spawn.targetTransform, Is.EqualTo(distant.transform));
                Assert.That(spawn.sourceCharacter, Is.EqualTo(main));
                Assert.That(spawn.attackerStats.maxHP, Is.EqualTo(2000f));
                var hitbox = (NormalHitbox)strike.ProjectileData.ResolveRoutineHitbox();
                Assert.That(hitbox.ResolveBaseDamage(spawn.attackerStats, 0f), Is.EqualTo(200f));
                string requiredNodeId = damageApplication.requiredNodeId;
                damageApplication.requiredNodeId = null;
                try
                {
                    typeof(ProjectileRuntime).GetMethod("ApplyHitEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(strike, new object[]
                        {
                            distant,
                            spawn.attackerStats,
                            new DamageProvenance(1, 1, 0, 0, null, hitbox.damageTags),
                            200f,
                            false
                        });
                }
                finally
                {
                    damageApplication.requiredNodeId = requiredNodeId;
                }
                Assert.That(distant.currentHP, Is.EqualTo(9840f), "Main's live 2000 Max HP adds a separate 8% Darkness hit after switching away.");
            }
            Assert.That(instance.nextTickAt, Is.EqualTo(5f));
            inventory.ClearRuntimeEffects(main);
            Assert.That(instance.formulaState, Is.Null, "Explicit removal must release the persistent field.");
            inventory.SetActiveTeamSlotIndex(0);
            player.SetCharacter(main, 2000f, main.stats);
            inventory.ApplyRuntimeEffect(main, recipe, main, main.stats);
            instance = inventory.GetActiveRuntimeEffects(main).Single(e => e.recipe.recipeId == recipe.recipeId);
            inventory.SetActiveTeamSlotIndex(1);
            player.SetCharacter(ally, 9000f, ally.stats);
            if (Application.isPlaying)
            {
                pool.ClearAllActiveProjectiles();
                inventory.TickRuntimeEffects(2.5f);
                Assert.That(instance.forceExpired, Is.True);
            }
            else
            {
                inventory.TickRuntimeEffects(15.1f);
                Assert.That(instance.IsExpired, Is.True);
            }
            Assert.That(managerHost.GetComponentsInChildren<ProjectileRuntime>().Length, Is.Zero);
            Assert.That(inventory.GetActiveRuntimeEffects(main), Is.Empty);
        }
        finally
        {
            SetInstance(typeof(ProjectileVFXManager), previousPool);
            SetInstance(typeof(ProjectileManager), previousManager);
            SetInstance(typeof(PlayerCharacter), previousPlayer);
            SetInstance(typeof(TeamLoader), previousTeam);
            Object.DestroyImmediate(teamHost);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(mainItem);
            Object.DestroyImmediate(allyItem);
            Object.DestroyImmediate(managerHost);
            Object.DestroyImmediate(targetHost);
            Object.DestroyImmediate(distantHost);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(main);
            Object.DestroyImmediate(ally);
        }
    }

    private static void SetInstance(System.Type type, Object value) => type.GetField("<Instance>k__BackingField",
        BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
}
#endif
