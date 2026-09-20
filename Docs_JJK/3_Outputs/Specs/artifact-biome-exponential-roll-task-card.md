---
slug: "artifact-biome-exponential-roll"
status: implementation-validation
source: manual
gdd_tags:
  - "core-loop"
  - "progression"
  - "items"
owner: "orchestrator-agent"
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Task Card: Artifact Biome Exponential Roll and Quality Upgrade

## Player-Facing Goal

Artifact rolls dynamically adapt to player biome depth. In early biomes (Biome 1), artifacts roll with baseline/lower quality weights. As players conquer bosses and advance to deeper biomes (Biome 2, Biome 3), the artifact roll system enters an exponential upgrade phase where high-to-max quality rolls become dominant.

## Source

- Origin: User prompt on 2026-09-06
- Reference: Exponential progression curve for artifact quality per biome.

## GDD Reference

- `@tag:core-loop` - Route rewards scale based on difficulty and progression.
- `@tag:progression` - Biome depth progression (`currentBiomeIndex`).
- `@tag:items` - Artifact rolls, amount bands, and normalized roll quality (0–100).

## Scope

### Systems Affected

- `ArtifactRollTable`: Added per-biome amount band weights and line count profiles.
- `ArtifactRollGenerator`: Biome-aware roll generation.
- `ArtifactInstance`: Persistence of `acquisitionBiomeIndex`.
- `PlayerRunInventory`: Biome propagation from `RunProgressionManager`.
- `InventoryAcquisitionResolver`: Biome forwarding.
- `ArtifactInstanceSystemTests`: Validation tests.

### Out of Scope

- Rarity colors or star levels (retains the existing numeric Roll Quality design).
- Modifying character stats or combat formulas directly.

## Acceptance Criteria

- [x] `ArtifactRollTable` supports configurable `ArtifactBiomeRollProfile` definitions including min-to-max amount bands and line count weights.
- [x] Biome 1 uses baseline low-biased weights (`[35, 30, 20, 10, 5]`), Biome 2 shifts mid-high (`[15, 25, 35, 20, 5]`), and Biome 3 enters an exponential upgrade phase (`[5, 10, 25, 35, 25]`).
- [x] Negative biome indices safely clamp to Biome 0.
- [x] Out-of-bounds biome indices safely clamp to the highest authored profile.
- [x] `ArtifactRollGenerator` respects active biome parameters when rolling amount bands and stat line counts.
- [x] `PlayerRunInventory.TryAcquireArtifact` automatically resolves active biome from `RunProgressionManager.Instance.CurrentBiomeIndex` with optional manual override.
- [x] `ArtifactInstance.acquisitionBiomeIndex` is preserved across cloning and persistent save/load.
- [x] Existing tests retain 100% backward compatibility.

