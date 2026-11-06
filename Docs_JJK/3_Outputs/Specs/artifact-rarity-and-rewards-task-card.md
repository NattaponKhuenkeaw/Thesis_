---
slug: artifact-rarity-and-rewards
status: needs-human
source: manual
gdd_tags: [core-loop, characters, items, ui, platform-input]
owner: project developer
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Artifact rarity and room rewards

Early Artifacts give players a small, readable way to shape a build. Hard routes trade immediate equipment for Soul, and later shops offer the nine existing premium passives. TeamBuilder makes individual copies easy to recognize and manage.

## Scope

- Feature with a supporting room-state refactor. Route through `/implement-feature` using the project workflow under `Docs_JJK/2_System_Files/Workflows/`.
- Source is the developer's planning request. The attached brief supplies proposed tuning and roster details. The two screenshots supply visual references.
- Plan covers the Artifact catalog and rolls, shop pools and pricing, Hard reward patterns, post-clear room access and draws, and TeamBuilder card and discard interactions.
- Save investigation is documentation only. Architecture approval allows feature implementation.

## Acceptance criteria for implementation

- [ ] Three visibly distinct rarities have phase-appropriate availability, roll distributions and Soul prices. All nine current definitions resolve from existing saves as SSR. Existing rolls and equipped references survive.
- [ ] Hard route previews match the total Soul and number of pulls granted. Each pull displays three complete choices, awards the selected copy once, and advances the queue. An Artifact-only route pays zero room-clear Soul.
- [ ] The encounter lifecycle gates interactions from room entry through the final reward. Empty rooms are accessible immediately after entry. Multi-chamber, boss, training and failure flows pass regression checks.
- [ ] TeamBuilder uses icon badges and a per-copy context menu. Safe discard removes exactly the intended copy and honours the confirmed PlayerPrefs opt-out.
- [ ] All new and reworked Artifact UI is authored as uGUI prefab objects through Unity MCP, with existing pixel UI assets and verified motion, text layout and input focus.

## Review gates

- [x] Design and game feel approval for the attached design spec. Approved by the project developer on 2026-11-05.
- [x] Architecture approval. Approved by the project developer on 2026-11-05.

See [the approved design](artifact-rarity-and-rewards-design-spec.md) and [architecture](artifact-rarity-and-rewards-arch-plan.md).

## Implementation handoff

The catalog, rarity profiles, shop pools, Hard route terms, sequential picker, clearing access, copy menu and discard preference are implemented. Unity MCP saved the new uGUI prefabs and updated inventory tile.

The final focused suite passed 44 of 44 checks. Saved prefabs were rendered and inspected at 16∶9 and 4∶3. The broader Artifact run passed 77 of 87 checks, with ten unresolved failures in older diagnostic and VFX fixtures. Human room-flow, controller and pacing playtests remain. See [the test plan](../TestPlans/artifact-rarity-and-rewards-test-plan.md).
