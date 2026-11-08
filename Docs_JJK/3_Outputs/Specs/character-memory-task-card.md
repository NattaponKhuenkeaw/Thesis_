---
slug: character-memory
status: approved
source: manual
gdd_tags:
  - characters
  - progression
  - ui
  - scene-roles
owner: implementation-agent
human_checkpoint: playtest
blocked_by: []
---

# Character Memory

Earn a permanent Memory for a living owned character after clearing a Hard combat room. The player picks a character by their Run Inventory portrait, then selects and confirms the next Memory. Major steps show Skill, Ultimate, and Passive cards. Generic steps show one card. The reward uses the Artifact picker artwork, animations, and audio cues. Characters with full tracks are omitted.

The shared track per character has 54 steps. A major choice is followed by two generic steps, for 18 major choices and 36 generic steps. Skill, Ultimate, and Passive each have six major upgrades. Cards for full families are visible and disabled. At the owner's target of three Memory rewards per biome, a full track takes 18 biomes.

## Implementation boundaries

- The existing character progression owner saves the shared track with the profile. Old Memory progress is capped at step 54 and existing family choices are retained where they fit the new caps. Main's combat effects use Memory ranks. His saved fragment ranks stay in the profile for compatibility. Other characters retain their fragment effects.
- The Hard reward table adds one Memory draw to each existing pattern. The room visit owns reward resolution and holds the exit until the reveal closes. A completed draw survives run rollback.
- The inspector's info page fills one box per earned major upgrade, using two rows of nine. Its Memory page uses the authored Potential header, background, full-screen scroll view, and node sizes. Step 1 starts at the bottom of the connected map. The map pans horizontally and vertically. The authored upgrade window previews Memory nodes, and **Previous upgrades** switches to the fragment-based nodes. The Shop uses the same Memory state.
- Each future major row previews one more rank per family, starting at the next unearned rank. The map shades projections beyond that family's cap and labels them **Unavailable**. Earned nodes keep their historical rank.
- Training Config opens the authored Memory window with Global Level in its footer and side arrows that cycle deployed characters. Its preview selects Memory steps in a session-only draft. Missing predecessors use the first available family in Skill, Ultimate, Passive order. Selecting a full family swaps with its latest assigned upgrade and keeps completed steps. Previous upgrades opens legacy rank controls for characters that use them. Apply commits the draft to the session and Cancel discards it.

## Playtest checks

- First Hard clear shows eligible character portraits. Choose a character, check all three major cards, then back out and verify progress is unchanged. Select a family twice to claim it, and check sound and visual feedback. Repeat for a generic step and check that the single card is centered.
- Open the inspector at different progress levels and check that the map starts at step 1. Pan across all three lanes, then use the wheel and controller to reach step 54. Preview a locked, next, and earned node and check that no upgrade controls appear.
- Test Hard payouts with zero, one, and two Artifact picks. Compare Soul to the route preview and try duplicate submission, closing and reopening the pause menu, and leaving the run after the grant.
- Load an earlier save, complete a Memory draw, then continue from the boss-lobby checkpoint. Verify the new step and previous upgrade effects persist.
- Select step 54 in Training, swap the first choice with a full family, then Apply. Reopen to check the full draft and enter Training again to check a fresh session.
- Open the Memory tab on the authored Inspector prefab. Verify the tab reopens without adding a second CanvasGroup, and compare the major-upgrade boxes before and after a generic reward.
- Count Memory draws across 6–8 biomes while choosing Hard routes. Compare steps and elapsed time to the pacing targets before authoring stat or kit effects.

## Main Memory migration

- Main's 18 major upgrades are authored on `CharacterObject_main` under **Kit Definition → Potential Nodes**. Memory ranks unlock those definitions, and both the reward card and map preview read the node description.
- Skill ranks add Dark Chain vulnerability, then weak pull at rank 2, followed by stronger pull at rank 3. Dark Chain does not stun, per the latest 2026-11-07 user correction in `projectile-magnetism-reaction-fix-task-card.md`. Rank 4 summons a chain that finds the nearest engaged enemy from the cast location every 2.5 seconds for 12 seconds, using ground projection. Rank 5 snapshots Main's Max HP when casting Dark Chain to buff the team. Rank 6 changes the base cooldown to 10 seconds. Summon settings and Passive rank 5 Ultimate finisher damage are authored on their corresponding nodes.
- Ultimate Memory 1 unlocks the authored Void Area follow-up after Jailbreak Punisher. Pulses use a five-second cadence, starting as the area activates. The seven-second vulnerability refreshes on hit. Rank 3 pays Main 5 AE per damaging pulse. Ranks 4–6 increase damage and duration.
- Passive Memory 1 opens the existing Normal Attack stages 4–5. Ranks 2–4 add Dimension Cut critical effects, Execution Breakdown, and Judgment Cut. Rank 5 upgrades Judgment Cut and adds Darkness damage to the two Ultimate finishers. Rank 6 refreshes a single Max HP buff on Ultimate cast.
- Generic steps use a fixed shuffled order so old saves and previews resolve the same bonuses. The 36 steps grant nine HP, seven ATK, four DM, four DI, and three each of CR, CD, CM, and ADI. Each step adds 3% of leveled base HP or ATK, or three percentage points to the corresponding base rate.
- Verify family unlock boundaries on a save with zero Memory and old fragment ranks. Confirm the 4–5 chain, the five-cut Judgment window, Dark Chain targeting behind Main, and Void AE payout with multiple enemies. Cast twice to confirm buffs refresh, and swap characters while the projectiles remain active.
