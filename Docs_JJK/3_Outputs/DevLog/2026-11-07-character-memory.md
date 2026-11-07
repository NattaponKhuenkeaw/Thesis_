# Character Memory progression

The character inspector now opens Memory first. The shared track has 60 steps per character, with a Skill, Ultimate, or Passive choice every third step starting at step one. The fragment-based ranks and their authored effects are available through Previous upgrades. The major-step effect conversion, role labels, and generic effects are deferred.

Each authored Hard reward pattern includes one draw. The room keeps the exit locked while the player picks an owned character and views the result. Progress is saved when the draw resolves and retained when the run rolls back to a lobby checkpoint. The Shop inspector reads the saved progress.

Training Config edits a separate draft. Selecting a later step fills missing prerequisites with Skill as the first choice. Editing an earlier choice keeps later steps, and Apply copies the draft into the current practice session.

Unity passed all five Memory tests and five legacy upgrade-panel tests. The broader run passed 30 of 32 tests. The Training catalog failure matches the existing Training report. The other failure occurred in the room-card WASD test with offers that have no reward. Pacing across biomes remains a playtest item.

## 54-step revision

The track now ends at step 54. Six major upgrades per family give 18 major steps and 36 generic steps. The Inspector shows 18 boxes in rows of twelve and six. Generic rewards leave the boxes alone.

The Memory panel now checks for the existing CanvasGroup through Unity's component lookup and adds one when absent. This avoids treating Unity's missing component reference as a valid C# object.

Older Memory saves retain completed steps up to step 54. The loader keeps recorded choices that fit the cap, then fills slots that had excess choices. Training uses the first family with room for prerequisites and swaps the latest matching choice when that family is full.

All 14 focused Unity tests passed. The suite reopened the Memory panel in Play Mode and checked the family caps, save normalization, and Inspector box layout.

The Inspector layout was then adjusted to two rows of nine at the owner's request.

Training Config now opens the same authored Memory window as the Inspector. Side arrows select a deployed character and the footer contains Global Level, Apply, and Cancel. The preview edits the practice draft, and Previous upgrades retains access to legacy practice ranks.
