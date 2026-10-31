---
slug: background-overlay-flash
status: draft
source: manual
gdd_tags: [visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Validation

Automated: phase boundaries and midpoint alpha; old serialized payload without new fields; zero/negative duration phases; timeline sum and rescaling; camera animation and legacy position lock with positional and rotational impulse noise, including a deferred Body pass. Flash checks live in ScreenImpulseCameraTests alongside camera feedback checks. The focused runner includes ScreenImpulseCameraTests, CameraAnimationSequenceTests and the four new MainDimensionSlashTests.CameraPositionControlPreservesImpulseNoise cases.

Run: `Tools > Project > Combat > Run Overlay and Camera Tests`. Results: `Temp/CombatPresentationTests.xml` and `Temp/CombatPresentationTests-summary.txt`.

Manual gameplay pass:

1. Open a Background Overlay Flash in the action editor; edit Fade In, Hold, Fade Out, Color and Peak Alpha, undo, save and reopen. Confirm the timeline duration follows the phase sum.
2. Play a visible fade-in/hold/fade-out; try zero phases and repeat while active. Confirm the final overlay becomes transparent and new impacts restart the envelope.
3. Run Camera Animation Control with Move/Hold Position; trigger an accepted HitData hit, then an ActionEventData Screen Impulse. Both should shake over the controlled position and settle to that position.
4. Repeat with legacy jump-cut lock, at room edges and during slow motion/full hitstop. Base composition remains bounded and flash uses real time.
5. End/interrupt the action, replace the camera event, switch characters and run an Ultimate overlay sequence. Confirm existing ownership/priority/visual-tail behavior remains intact and enemy telegraphs remain readable.

Human gameplay checks remain pending; automated completion is recorded in the DevLog.
