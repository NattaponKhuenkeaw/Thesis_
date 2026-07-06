# DevLog: 2026-07-05 - Character Potential Panel

## Goal
Implement the Character Potential full-screen panel from Character Info, including node selection, fragment requirements, upgrade confirmation, and permanent upgrade persistence.

## What I Did
- [x] Added black fade page transitions for Potential entry/exit in `InspectScreenUI`.
- [x] Reworked `CharacterPotentialPanel` around Header, Content, and Upgrade Window responsibilities.
- [x] Added `CharacterPotentialNodeUI` for 12 track nodes: Skill, Ultimate, Passive, levels 1-4.
- [x] Added `CharacterPotentialUpgradeWindow` for pre-upgrade and upgraded states.
- [x] Kept permanent upgrade state in `PlayerMetaProgression`, keyed by `CharacterObject.characterId`.
- [x] Updated `CharacterInfoPanel` fallback routing to pass the selected character into Potential.

## Key Decisions
- Used `PlayerMetaProgression` as the permanent Potential authority because fragments and potential levels already live there.
- Left drag behavior to Unity `ScrollRect`/layout wiring; the code only binds content and does not implement manual drag.
- Added runtime fallback UI so existing prefabs still function before a final authored layout pass.

## Bugs Found
- [x] Existing Potential UI only summarized tracks and upgraded by track button, so node selection and per-node preview did not exist.

## Game Feel Notes
- The Info/Potential black fade is functional but should be reviewed in Unity for timing and visual polish.
- Final node line art/connection styling should be authored in the prefab for best presentation.

## Next Session
- Wire final prefab references for Header labels, node art, connection lines, ScrollRect bounds, and upgrade-window art.
- Playtest fragment counts and cost readability with real character assets.

---

## Git Commit Summary
_Copy-paste this into your commit message:_

```
feat(ui): implement character potential panel

- add full-screen Potential page fade from Character Info
- bind 12 potential nodes from CharacterObject kit data
- add fragment-gated upgrade window backed by PlayerMetaProgression
```
