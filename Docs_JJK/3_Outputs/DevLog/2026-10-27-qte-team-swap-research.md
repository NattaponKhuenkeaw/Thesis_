---
slug: qte-team-swap
status: approved
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE Team Swap research

Researched WuWa's developer-described entry skills and team rhythm, HI3's documented condition-driven entry/UI, and inspected the live Thesis_ Unity project's current swap, clone, HUD and character attack data. User clarified that Main's X-3 and CameraGirl's X-1 positions are enemy-relative.

Prepared the task card, researched design spec, exact authored attack mapping and validation plan. Identified the recovery-gated swap, automatic combat-swap slow motion and hard clone lifetime cap as relevant continuity risks. Verified that CameraGirl Attack4's event is actually AfterImage despite its legacy “Ultimate Sequence” label.

No runtime or canonical GDD changes made. No gameplay tests run. Existing local edits preserved. Awaiting the design/game-feel checkpoint required by the feature workflow; architecture and runtime implementation remain subsequent work.

## Implementation and playtest revision

Apricha approved implementation and subsequently requested outgoing-relative placement and a super-slow intro handoff. Implemented both: offset uses outgoing facing and position, then incoming faces the enemy; slowdown starts at 0.08x for 0.24 real seconds, saved on the Player prefab TeamManager component. Exact attack steps remain referenced, with a terminal intro node, hit-confirm swap legality, five-second refresh-only readiness, independent white portrait cue, execution provenance, outgoing active-hit history and targeted-motion continuity.

Verification: Unity compilation clean; QTE tests 22/22 passed; wider selected suite 105/107 passed. The two failures concern an existing CameraGirl buff expectation (33 versus authored 43) and an EditMode hitstun fixture with uninitialized state. Recorded evidence and portrait render are in Reports/QteTeamSwap. No scene saves or build/dependency changes. Final subjective feel is available for human retest.

## Camera reset follow-up

Apricha requested resetting CameraLookAhead when the incoming intro begins, with E2E testing handled personally and no scripted tests. The swap handoff now rebinds the proxy to the incoming Rigidbody, clears old horizontal/fall offsets, damping velocities and directional tweens, and holds the proxy on the incoming position for the real-time intro slowdown duration. CameraManager temporarily centers player framing with zero target offset and no follow damping, preserving enemy registration and room confinement, then restores normal framing and eases look-ahead back from zero. Only compilation/console verification is performed for this follow-up; the earlier test counts above predate it.

### Superseding camera revision: horizontal only and combat group framing

Apricha refined this behavior to reset X only and retain the Combat camera's TargetGroup. The intro now clears and recenters horizontal look-ahead while preserving vertical offset, vertical damping velocity and fall anticipation. It no longer switches to a temporary player-only camera or alters the PositionComposer target offset. The existing player/proxy group member is refreshed and TargetGroup bounds are updated immediately, so the new spawn position contributes to automatic group framing/zoom within existing authored and room limits. Horizontal look-ahead resumes from zero after the handoff duration. No scripted tests were added or run; E2E remains with Apricha.

### TargetGroup revert

At Apricha's request, removed the swap-specific TargetGroup membership/bounds refresh. Normal TargetGroup framing/zoom is left to its existing update flow. The QTE hook only resets CameraLookAhead on X for the incoming character; vertical follow and offsets remain continuous. No forced player-only camera, group zoom adjustment, or scripted tests. Compilation/console check only; Apricha handles E2E.

### Intro size impact

Apricha requested reducing camera projection/size by 1 for 1-5 frames at intro start. Added an editable size reduction of 1 and starting duration of 3 rendered frames on Player prefab > TeamManager > QteTeamSwapController. A Cinemachine extension applies the orthographic-size subtraction after group framing, without changing authored lens values or accumulating reductions; normal camera size resumes automatically after the frame window. Rendered-frame duration prevents the effect stretching with the intro slow motion. Existing player-tuned introSlowMotionSeconds=1 was preserved. E2E criterion: the snap should add a readable impact without obscuring the handoff; increase toward 5 frames if too subtle, decrease toward 1 if too abrupt. No scripted tests; compilation/console verification only.
