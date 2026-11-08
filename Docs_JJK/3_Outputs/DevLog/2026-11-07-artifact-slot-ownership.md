# Artifact slot and ownership feedback

User-requested follow-up to artifact UI polish: make the destination brighter with a pulse, preview equipment when selecting occupied slots, preserve inventory state across slot clicks, and show ownership without losing rarity color.

The selected-slot toggle and preview reset caused the disruptive close/reset behavior. Equipped cells also replaced rarity tint with gray. Slot clicks now update destination and preview only; owner portraits communicate equipment state while rarity tint stays intact. Tall owner art is cropped into a square badge; Favourite moves below it. Preview stat cleanup supports repeated editor rendering as well as Play Mode.

Unity compilation and 86 prefab interaction/layout assertions passed. Checked 16:9 and 4:3 captures. No commit, merge, scene save, dependency or build-setting change. Existing unrelated work was preserved. Human visual/game-feel review remains available through the regression checklist.

Evidence: `../Reports/ArtifactUiPolish/2026-11-07-slot-ownership.md`.
