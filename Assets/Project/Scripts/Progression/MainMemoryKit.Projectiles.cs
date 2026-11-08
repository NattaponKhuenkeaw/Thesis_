using System.Collections;
using Project.Combat;
using UnityEngine;

namespace Project.Progression
{
    internal static partial class MainMemoryKit
    {
        internal sealed class ProjectileEffects
        {
            internal float AttackRatio;
            internal float MaxHpRatio;
            internal float AuraEnergy;
            internal RuntimeEffectRecipe Vulnerability;
            internal bool LimitToTarget;
            internal bool FixedCadence;
        }

        private sealed class Projectiles
        {
            internal ProjectileData StrongChain;
            internal ProjectileData SummonedChain;
            internal ProjectileData ShortVoid;
            internal ProjectileData LongVoid;
            internal ProjectileEffects[] SkillEffects;
            internal ProjectileEffects[] VoidEffects;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharacterObject, Projectiles>
            ProjectileCache = new();

        internal static void ConfigureProjectile(ref ProjectileSpawnContext context, CharacterPassiveController passive)
        {
            if (!AppliesTo(context.sourceCharacter) || context.projectileData == null || passive == null) return;
            var variants = ProjectileCache.GetValue(context.sourceCharacter, _ => new Projectiles());
            if (context.sourceActionId == DarkChain)
            {
                int skillRank = passive.GetPotentialLevel(PotentialTrack.Skill);
                if (skillRank == 0) return;
                if (skillRank >= 2)
                {
                    ProjectileData weak = GetNode(context.sourceCharacter, PotentialTrack.Skill, 2)
                        ?.actionModifiers.Find(modifier => modifier.replacementProjectileData != null)?.replacementProjectileData;
                    if (weak != null)
                    {
                        if (skillRank >= 3 && variants.StrongChain == null)
                        {
                            variants.StrongChain = CopyProjectile(weak);
                            variants.StrongChain.magnetismSpeed = weak.magnetismSpeed * 2f;
                        }
                        context.projectileData = skillRank >= 3 ? variants.StrongChain : weak;
                        context.projectileType = context.projectileData.projectileType;
                        context.remainingHits = context.projectileData.maxHits;
                    }
                }
                variants.SkillEffects ??= new[]
                {
                    new ProjectileEffects { Vulnerability = GetNode(context.sourceCharacter, PotentialTrack.Skill, 1)?.projectileHitEffect },
                    new ProjectileEffects
                    {
                        Vulnerability = GetNode(context.sourceCharacter, PotentialTrack.Skill, 3)?.projectileHitEffect
                    }
                };
                context.memoryEffects = variants.SkillEffects[skillRank >= 3 ? 1 : 0];
            }
            else if (context.sourceActionId == Jailbreak && context.sourceStep?.stepName == VoidStep)
            {
                int rank = passive.GetPotentialLevel(PotentialTrack.Ultimate);
                if (rank == 0) return;
                if (variants.ShortVoid == null) variants.ShortVoid = CreateVoid(context.projectileData, 20f);
                if (rank >= 5 && variants.LongVoid == null) variants.LongVoid = CreateVoid(context.projectileData, 40f);
                context.projectileData = rank >= 5 ? variants.LongVoid : variants.ShortVoid;
                variants.VoidEffects ??= BuildVoidEffects(context.sourceCharacter);
                context.memoryEffects = variants.VoidEffects[rank - 1];
                context.auraEnergyRewardClaim = null;
            }
        }

        private static ProjectileEffects[] BuildVoidEffects(CharacterObject character)
        {
            var vulnerability = GetNode(character, PotentialTrack.Ultimate, 2)?.projectileHitEffect;
            var ranks = new ProjectileEffects[CharacterMemoryProgress.UpgradesPerFamily];
            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i] = new ProjectileEffects
                {
                    AttackRatio = i >= 5 ? 10f : i >= 3 ? 5f : 2f,
                    MaxHpRatio = i >= 5 ? .12f : i >= 3 ? .06f : 0f,
                    AuraEnergy = i >= 2 ? 5f : 0f,
                    Vulnerability = i >= 1 ? vulnerability : null,
                    FixedCadence = true
                };
            }
            return ranks;
        }

        private static ProjectileData CreateVoid(ProjectileData source, float lifetime)
        {
            ProjectileData data = CopyProjectile(source);
            data.lifetime = lifetime;
            data.hitIntervalSeconds = 5f;
            data.routineHitbox = CopyRoutine(source, ElementType.Void);
            return data;
        }

        internal static IEnumerator SummonChain(ProjectileManager manager, ProjectileSpawnContext original)
        {
            ProjectileVFXManager pool = ProjectileVFXManager.Instance;
            if (pool == null || original.projectileData == null) yield break;
            var settings = GetNode(original.sourceCharacter, PotentialTrack.Skill, 4)?.darkChainSummon;
            if (settings == null) yield break;
            int clearVersion = pool.ClearVersion;
            var variants = ProjectileCache.GetValue(original.sourceCharacter, _ => new Projectiles());
            if (variants.SummonedChain == null)
            {
                variants.SummonedChain = CreateSummonedChain(original.projectileData, settings);
            }

            var effects = new ProjectileEffects
            {
                AttackRatio = settings.attackRatio, MaxHpRatio = settings.maxHpRatio, LimitToTarget = true,
                Vulnerability = original.memoryEffects?.Vulnerability
            };

            var strike = original;
            strike.projectileData = variants.SummonedChain;
            strike.memoryEffects = effects;
            strike.remainingHits = 1;
            strike.auraEnergyRewardClaim = null;
            strike.additionalDamageEffects = null;
            float start = Time.time;
            float interval = Mathf.Max(.1f, settings.intervalSeconds);
            for (float time = interval; time < settings.durationSeconds; time += interval)
            {
                while (Time.time < start + time && pool != null && pool.ClearVersion == clearVersion)
                    yield return null;
                if (manager == null || pool == null || pool.ClearVersion != clearVersion || Time.time >= start + settings.durationSeconds)
                    yield break;

                TeamLoader team = TeamLoader.Instance;
                if (team != null && (team.RunInventory == null || !team.RunInventory.IsRunCharacterAlive(original.sourceCharacter)))
                    yield break;
                var enemy = CameraManager.instance?.GetNearestEngagedEnemyInRadius(original.origin);
                if (enemy == null || !enemy.IsTargetable) continue;
                strike.targetTransform = enemy.transform;
                strike.direction = enemy.transform.position.x < original.origin.x ? Vector2.left : Vector2.right;
                manager.SpawnProjectile(strike);
            }
        }

        private static ProjectileData CreateSummonedChain(ProjectileData original,
            PotentialNodeDefinition.DarkChainSummonSettings settings)
        {
            ProjectileData template = settings.projectile != null ? settings.projectile : original;
            ProjectileData data = CopyProjectile(template);
            data.spawnPositionMode = SpawnPositionMode.Target;
            data.spawnReferencePointMode = settings.referencePoint;
            data.applyOffsetToTarget = false;
            data.traversalMode = ProjectileTraversalMode.Linear;
            data.speed = 0f;
            data.lifetime = Mathf.Max(.1f, settings.strikeLifetimeSeconds - (data.spawnClip != null ? data.spawnClip.length : 0f));
            data.magnetismSpeed = 0f;
            data.maxHits = 1;
            data.hitCountMode = ProjectileHitCountMode.Shared;
            data.destroyWhenHitsExhausted = false;
            data.spawningHitbox = new ProjectilePhaseHitboxData();
            data.routineHitbox = CopyRoutine(template, ElementType.Darkness);
            ((NormalHitbox)data.routineHitbox.hitbox).offset = Vector2.zero;
            return data;
        }

        private static ProjectilePhaseHitboxData CopyRoutine(ProjectileData source, ElementType element)
        {
            var hit = ((NormalHitbox)source.ResolveRoutineHitbox()).Copy();
            hit.elementType = element;
            return new ProjectilePhaseHitboxData { enabled = true, decrementsMaxHits = true, hitbox = hit };
        }

        private static ProjectileData CopyProjectile(ProjectileData source)
        {
            ProjectileData data = Object.Instantiate(source);
            data.hideFlags = HideFlags.DontSave;
            return data;
        }

        internal static AdditionalDamageEffectDefinition ResolveFinisher(CharacterObject character,
            CharacterPassiveController passive, string actionId, AttackStepData step, NormalHitbox hitbox,
            ProjectileRuntime projectile)
        {
            if ((actionId != Jailbreak && actionId != Execution) || !AppliesTo(character) ||
                hitbox == null || projectile != null || step?.hitData?.hitboxes == null)
                return null;
            int rank = passive != null && passive.playerCharacter?.characterObject == character
                ? passive.GetPotentialLevel(PotentialTrack.Passive)
                : TeamLoader.Instance?.MetaProgression?.GetMemoryRank(character.characterId, PotentialTrack.Passive) ?? 0;
            if (rank < 5) return null;
            if (actionId == Jailbreak && !hitbox.breaksGrab) return null;
            foreach (HitboxBase candidate in step.hitData.hitboxes)
                if (candidate is NormalHitbox && candidate.startFrameOffset > hitbox.startFrameOffset) return null;
            return GetNode(character, PotentialTrack.Passive, 5)?.ultimateFinisherDamage;
        }
    }
}
