---
slug: main-void-area-particles
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Scoped particle implementation

Reuse `ProjectileVFXManager` pooling and `ProjectileRuntime` lifetime/area authority. `GenericProjectilePlayer` caches an optional `VoidAreaParticleVisual`, begins it after runtime initialization with the resolved ProjectileData/context, and clears it when preparing for the pool. Other projectile prefabs have no such component.

`VoidAreaParticleVisual` configures cached emitter references once per spawn using routine-hitbox geometry and context.sizeMultiplier. Local-space particles follow the stationary projectile; the visual root anchors at ground Y, while X follows the hitbox offset. No frame polling or allocations are needed. ParticleSystem handles time and motion. Clear on disable prevents trails leaking into a subsequent pool use.

A dedicated prefab contains GenericProjectilePlayer, ProjectileRuntime, safe 2D physics and five ParticleSystems (ground glow, flame, smoke, black halo and floating flame fragments), without Animator/SpriteRenderer. A dedicated URP unlit particle shader/material supplies procedural moving flame/smoke, a soft shadow halo, and pointed dark fragments with amethyst edges. ParticleSystem noise, rotation, lifetime color and size curves handle fragment curl/dissolve without C# frame polling. An editor authoring/preview utility creates only these assets, updates only the Void Area projectile definition's visual fields if wiring is needed, and captures in an isolated preview scene.

Data flow: action spawn -> resolved projectile definition/context -> existing runtime initialization -> optional particle Begin -> runtime expiration -> existing pool return -> particle Clear. No new state, damage delivery or lifecycle authority; no new ADR required. The implementation is a locally reviewable realization of the user's requested particle replacement; architecture/feel acceptance remains with the human review.
