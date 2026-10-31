---
slug: action-window-icons
status: approved
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: manual playtest
next_agent: human
blocked_by: []
---

# Action icon runtime fallback and DashData art

Apricha reported that Main's authored action sprites still appeared as fallback or
missing during play, and requested a Dash icon on DashData.

Inspected the live Test_Playground UI through Unity. NA, Skill and Ult Images had
null sprites at runtime. The prefab and Main resolver table referenced GUID
`66fb99481ac3d2d43af7a43990b1a4ef`, but that asset was absent; the current replacement
is `UI_Attack_Basic 1.png` with a different GUID. Repointed the fallback images,
serialized fallback field, and Main's Ground/Air action icons to the replacement
sprite's first sub-asset. The explicit fallback field also prevents a null Image
sprite from becoming a null cache entry. Timed action icons remain gated by the
matching resolver context and live window.

Added optional `DashData.icon`, exposed it in the character action authoring panel,
assigned Main's existing `UI_Dash_Normal.PNG`, and bound the active character's icon to
the current Dash slot. If unassigned, Dash keeps its existing prefab sprite.

Manual validation performed: before the fix, Play mode reported null sprites for
NA/Skill/Ult while Dash was valid. After reimport, Play mode reported the replacement
sprite path on NA/Skill/Ult and the assigned Dash sprite on Dash. Unity compilation
completed without errors; existing obsolete Artifact API warnings remain.

Follow-up investigation after Apricha reported that timed-window art appeared but the
ordinary NA/Skill/Ult resolver icons did not: `ActionResolver.ResolveInitial` can set
`ActiveCombatAction` before returning no executable step. `PlayerCharacter.TryResolveActionForContext`
returns `false` for that no-step result even though its `action` output is valid. The
HUD incorrectly treated the bool as proof that no action was selected and kept the
fallback sprite. `ResolveActionDisplay` now uses the returned action independently
from step success. Context and authored conditions still control selection; the timed
overlay still requires its matching live availability window.

The connected Test_Playground runtime instance used for diagnosis had
`PlayerCharacter.characterObject == null`, so it could not be used to confirm a loaded
character's resolver icons in Play mode. The script refresh completed with no Unity
console errors; manual playtest remains outstanding.
