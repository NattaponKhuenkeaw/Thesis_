---
slug: main-qte-lightning-plunge
status: needs-human
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Lightning Plunge — Implementation and Tuning

Implemented after separate design and architecture approvals on 2026-10-28. Apricha then playtested and requested larger/longer impact VFX, Heavy tier and a camera correction. **Latest requested height is Y+5**; it supersedes the temporary Y+10. Current revision workflow: `/fix-bug`. The approved initial feature workflow was `/implement-feature`.

## Ground shockwave rendering repair - 2026-10-29

Ground shockwave rendering repair (2026-10-29) — supersedes disabling it: Apricha explicitly requested fixing the ground wave, not hiding it. Restored Ground Shockwave Enabled=true in Main's saved asset and class default. The shared procedural energy shader no longer samples the per-renderer sprite _MainTex: mesh/particle geometry already supplies UV/color and does not need an external texture, eliminating a potentially stale atlas-texture dependency. Alpha is saturated and emitted RGB is nonnegative. This removes a plausible colored-artifact path; it is not a live reproduction proving that binding was the sole cause of the reported green/black ring.

Rebuilt Arc as a joined ellipse ribbon with analytic edge normals shared between segments, clamped nonnegative taper and explicit zero-width endpoints. This avoids independent overlapping/turned wedges at flat ellipse tips. WindArcs uses the existing soft additive glow material in runtime initialization, prefab and authoring. The expanding ground ring, wide ground glow, lightning, large explosion, purple accents and existing exit motion are all retained. Damage, timing, camera and placement are unchanged.

Validation: Runtime and Editor C# compilation using Unity's cached response files and bundled Roslyn, with output DLLs only in OS temp; no game assemblies overwritten. No automated tests added or run. Unity MCP remains unavailable on localhost:8080; both Windows UI tool entry points also failed to initialize (missing kernel-assets path). Therefore shader import/GPU rendering and the exact Game-view reproduction remain unverified. Manual acceptance: reimport in Unity and repeat the Main hit; ring must visibly expand and fade purple with no green/black band. Do not treat the prior disable pass as resolution.

## Current behavior

Main's prepared QTE uses captured enemy-relative entry (latest saved user tuning observed during the atomized-exit revision: X -1.5 / Y 5), physical vertical plunge and a verified floor contact before the existing hit pipeline. The thunderstrike has **two pre-impact logic ticks**, impact at tick 3, and a long near-vertical bolt extending above the visible camera top. Fine kinks/forks replace the old winding body. **Main's intro slow motion is restored** after the user rejected its removal: it uses the existing QteTeamSwapController values, saved on the Player prefab as 0.08× for 0.5 real seconds when inspected. The class default duration is 0.24 seconds; saved prefab tuning is retained. Other characters' intro behavior and normal hitstop/pause remain authoritative.

The body is hidden during flight, revealed in its landing pose at impact and sampled into Standing during the saved recovery. Existing normal attack continuation executes the saved Execution Breakdown next node. No new resource charge or damage multiplier was added.

The ground effect uses impactScale 2.5 while the falling bolt stays narrow. Readable impact hold is 18 ticks. Existing light, column, wind and residual settings remain independently editable. The current action timing is 2 startup / 24 active / 26 recovery (52 ticks total); the longer cosmetic tail now continues after normal completion or authored follow-up, with no added damage or action lockout. Cancellation and disable clear it.

The latest revision restores the earlier broad explosion silhouette at Apricha's request. Thick irregular dark-purple lobes and bright white/violet branches radiate around the long column. Their exit opens from the contact point toward the outer tips, with lateral drift and upward float. The lobes become shorter/sparser at staggered times and fade, while low wind spreads. No grain, shatter, noise dissolve, overall root-scale animation or separate curved sparkle-wisp pass. Ground-plane clamping prevents solid lobe geometry drawing below the impact surface.

Current cosmetic starting values: saved hold 18 / delay 0, exit duration 78, scatter 0.65, lift 0.6; sparks 18, debris 10, residual sparks 4. Current saved action and entry tuning remain untouched. Cosmetic tail ownership, interruption cleanup, action-clock sampling and normal follow-up behavior remain as implemented previously.

The reported colored artifact prompted inspection of both global and action-local chromatic paths. DefaultVolumeProfile had ChromaticAberration active at intensity 0.1; it is now inactive with intensity 0. Main's saved cut chromaticPixels strengths (2/3) are now 0, including follow-up effects; new distortion profiles default to 0. Main plunge's optional refraction is disabled, and MainCutImpactAuthoring preserves that disabled state. The shared cut refraction/blur capability remains for other authored effects. No dependencies or renderer-feature configuration changed in this correction. Procedural Slash remains free of noise dissolve.

Secondary purple electricity now surrounds both phases: short angular arcs with occasional forks, bright HDR purple cores and soft additive halos pulse beside the falling bolt and around the impact perimeter. Eight sparse falling sparks use the existing ImpactSparks Particle System. The existing broad blast/exit is unchanged. The formerly unused GroundCracks geometry layer carries accent cores, with existing VioletEnvelope / SoftGlow / GroundGlow layers supplying edge and halo; prefab hierarchy is unchanged. No chromatic splitting, screen-space distortion, new package/material or whole-object scaling was added.

The QTE root's NormalHitbox is **Heavy**, preserving its existing damage ratio, type, dimensions and reaction data. The much larger effect is cosmetic; the actual hitbox has not been enlarged to match its wind/crack reach.

Camera correction follows Main's actual body plus an editable **1.5 world-unit** offset during flight, retains the pre-handoff orthographic size and cancels the outgoing camera blend. At contact or early release, it blends position and lens over an editable **0.45 scaled seconds** to the current gameplay camera. That destination is evaluated continuously so exploration/combat framing or Execution Breakdown can become the next owner. The return continues independently of the old combat execution and does not reset composer/group damping history on release. The former `orthographicSize × 0.65` height minimum is removed. The requested midair lock can exceed the room's vertical camera ceiling; supported horizontal box confinement remains. This is deliberately local to Main's plunge, not a global change to ordinary QTE zoom or room cameras.

## Assets and hierarchy

Main asset: `Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset`.

Reusable prefab: `Assets/Project/Character/Hero/test_0_Main/LightningPlunge/LightningPlungeVFX.prefab`.

```text
LightningPlungeVFX [LightningPlungeVFX]
  Geometry
    BoltCore
    VioletEnvelope
    DarkImpactShards
    GroundCracks
    WindArcs
    DarkPurpleAura
    SoftGlow
    GroundGlow
  Particles
    ImpactSparks
    Debris
    ResidualSparks
  Impact Light (URP 2D)
```

The effect reuses buffers, mesh instances and Particle Systems. The new **Project/LightningPlungeEnergy** shader provides smooth UV-based edge alpha, optional radial falloff, HDR emission and combat-clock filament variation. Saved materials split the bright additive core, broad additive halo, low-alpha radial pool and dark-purple alpha aura. The aura's main column is one continuous ribbon to prevent dark overlap stripes between bolt segments. Cosmetic particles are manually sampled with action time so pause/hitstop do not advance them independently.

A local URP Point Light2D pulses at confirmed impact and fades with the action clock. It lights compatible lit sprites in its configured sorting layers; unlit sprites will not receive that lighting. The visible halo is shader geometry and works without enabling global Bloom. No package, downloaded texture, renderer-feature or scene-wide volume/pipeline setting was added. The effect's own prefab, shader and materials provide the new appearance.

The asset folder also contains `main_qte_lightning_plunge_actor.anim` and a QTE-only ground-aligned landing sprite asset reusing the existing texture pixels. Main's controller has a zero-speed state sampled from action logic. The original actor clips remain source art.

## Tuning

Select Main's CharacterObject in Unity and expand **Qte Swap → Plunge**:

Electrical accents are exposed separately under **Purple Electrical Accents**: Falling Arc Count 6, Impact Arc Count 9, Spread 1, Arc Length 0.8, Width 0.028, Glow 1.2, HDR purple tint, Falling Spark Count 8. These are tunable starting values. Reduce count if arcs compete with the broad silhouette; increase Glow for stronger soft halos rather than increasing flash area. **Add Main Plunge Electrical Accents** saves only these controls.

| Setting | Current value / effect |
| --- | --- |
| Ground Shockwave Enabled | ON for Main: restored expanding wind rings and wide ground pool, using repaired procedural rendering |
| Target Start Offset | Enemy-relative, X follows approach facing. Latest saved user tuning X -1.5 / Y 5 is preserved; older offsets below are historical. |
| Camera Actor Offset Y | 1.5 world units above the body root, independent of zoom |
| Camera Return Seconds | 0.45 scaled seconds, starting at contact or early release; blends toward the live preferred camera |
| Visuals / Scale | Overall shape size, including falling bolt |
| Visuals / Impact Scale | 2.5; ground splash, column, cracks, wind and particle reach |
| Ground Spread / Impact Height | Base radius 3.3 and column height 2.5 before impact scale |
| Impact Hold Frames | 18; keep the silhouette at full strength before fading |
| Column / Light Fade Frames | 54 / 36 after hold; replaces earlier hard-coded 14 / 18-tick fades |
| Flash / Burst / Wind / Residual Frames | 10 / 90 / 108 / 118 logic ticks; burst fade follows the hold |
| Exit Delay Frames | Saved 0 after the 18-tick hold; later delays the outward release |
| Exit Frames | 78-tick starting value for the broad blast clearing motion |
| Exit Scatter | 0.65 additional outward wavefront travel relative to impact radius |
| Storm Lift | 0.6 upward drift of clearing explosion lobes before impact scale |
| Sparks / Debris / Residual | 18 / 10 / 4 supporting particles; impact silhouette comes from broad geometry |
| Core / Envelope Width | Bright path thickness |
| Minimum Bolt Length / Above Screen Padding | 14 / 2 world units; the upper endpoint also follows the camera top so the strike enters from outside the frame |
| Bolt Jitter | 0.12 world units; bounded side deflection, not a fraction of bolt length |
| Glow Width | 3.8 × envelope width; soft additive halo around the bright core |
| Aura / Glow / Core Color | Dark-purple absorption, low-alpha emissive halo and HDR white-violet center |
| Impact Light Intensity / Radius | 0.8 / 7 starting values; local URP 2D light on compatible lit sprites |
| Palette / Branches | White/violet/dark shapes and breakout silhouette |
| Spark / Debris / Residual Count | Density; tune after shape/contrast |
| Sorting Layer / Order | WorldUI / 995, with small layer offsets per geometry/particle group |
| Seed | Zero varies locally; nonzero makes repeatable previews |

Tune action startup/active/recovery and hitbox tier through the existing animation/action editor's **Main QTE** tree. The timeline remains **2 / 3 / 120** logic ticks (125 total), with impact at tick 3 and standing at tick 14. The two-tick strike now plays through the restored cinematic intro slowdown, so two logic ticks do not mean two real-time rendered frames during that slowdown. Pose, physical endpoint and contact audio remain aligned. The isolated preview reads actual saved phase boundaries and plays at normal logic speed; it does not apply the team handoff slowdown.

Use **Tools → Project → Combat → Preview Main Lightning Plunge** to scrub or play an isolated visual preview. It does not alter the saved gameplay scene. **Capture Main Lightning Plunge Phases** writes phase PNGs in this report folder. **Author Main Lightning Plunge** reproduces the prefab/clip/root while retaining the existing follow-up and saved profile tuning. **Apply Main Plunge Impact Revision** explicitly reapplies Y+5 and the current size/tail defaults; this is an intentional tuning reset, not something to run after every manual edit.

**Polish Main Thunderstrike** explicitly reapplies the two-tick startup and dark-purple/HDR palette while preserving current height, camera settings, Heavy damage data, recovery and follow-up. **Lengthen Main Thunderstrike Impact** applies only the latest hold/fade values; it does not regenerate palette, actor clip, motion or camera settings. These are revision authoring commands, not automatic runtime resets. Shader/material controls include `_Emission`, `_Softness`, `_Profile` (ribbon/radial) and `_DstBlend` (additive/alpha). No reference image pixels/text were imported into the project.

**Author Main Plunge Dissipation** now applies the broad blast exit starting values and disables Main plunge refraction plus Main's saved cut RGB split. It preserves saved hold/delay and gameplay tuning. No fragment or sparkle-wisp group is created. **Capture Main Lightning Plunge Motion** renders sequential preview frames to the OS temp folder without changing gameplay scenes.

Standalone use: obtain/reuse a LightningPlungeVFX prefab instance, call `Begin(settings, start, contact, surfaceNormal, seed)`, then `Sample(actionFrame, impactFrame, endFrame)` from its owner's clock and `Stop()` before release. It has no damage, target search, camera or actor visibility authority. The QTE state manages those gameplay concerns.

For a custom preview camera, call `SetPresentationCamera(camera)` after Begin so above-screen geometry uses that view; this is a read-only camera reference and never moves the camera. Runtime defaults to Camera.main. Main column aura/halo use continuous ribbons; fine electrical paths/forks retain bounded kinks.

## Files

- Data: `Scripts/Character/QteSwapDefinition.cs`, `QtePlungeSettings.cs`, `QtePreparedEntry.cs`, `LightningPlungeVfxSettings.cs`, appended `MotionTaskType.CapturedWorldPoint` in `MotionFrameData.cs`.
- Entry/motion: `Scripts/Combat/QteTeamSwapController.cs`, `PlayerMovement.cs`, `PlayerMovement.QtePlunge.cs`, `Scripts/Systems/TeamLoader.cs`.
- State/presentation: `Scripts/Player/States/PlayerQteIntroState.cs`, small abort-output hook in `CombatPlayerState.cs`, `Scripts/Combat/LightningPlungeVFX.cs`.
- Camera: `Scripts/Cameras/CameraManager.QtePlunge.cs`, `QteIntroCameraImpact.cs`.
- Editor: `Scripts/Editor/MainLightningPlungeAuthoring.cs`, `LightningPlungePreviewWindow.cs`, entry-builder delegation in `ExecutionBreakdownAuthoring.cs`.
- Authored data: Main CharacterObject, Main controller and the new LightningPlunge asset folder. Existing unrelated working-tree edits were not reverted, staged or committed.
- Thunderstrike polish: `LightningPlunge/LightningPlungeEnergy.shader`, `LightningPlunge.mat`, `LightningPlungeAura.mat`, `LightningPlungeGlow.mat`, `LightningPlungeGroundGlow.mat`, and updated prefab light/geometry layers. The shader is new; the earlier unlit-material-only appearance is retained only in historical notes/captures.

All script paths above are relative to `Assets/Project/`. Unity generated actual asset GUIDs/metas; none were fabricated.

## Validation and limitations

**Electrical-accent validation:** Compiled and captured 41 sequential Unity preview frames. Inspected falling frame 0, impact frame 15 and tail frame 54; purple forks/halos are visible around the approved blast. Fresh [electrical-motion.gif](electrical-motion.gif), electrical-falling.png, electrical-impact.png and electrical-tail.png contain this revision. Standard phase capture encountered a Windows mapped-file lock on the old phase-000.png; motion capture used a separate output folder successfully. No new compile/invalid-bounds errors appeared in the completed motion capture. Existing unrelated editor NullReferenceException persists. No automated tests added or run; live readability and pause/cancel/reuse remain human review items.

**Latest exit-animation validation:** Unity compiled. Isolated phases 14, 55 and 72 were inspected for the restored broad blast, opening center and floating smaller lobes; 41 sequential frames regenerated [storm-motion.gif](storm-motion.gif). Saved Main cut chromatic strengths were checked to be 0, the plunge refraction flag is off and the default profile's chromatic override is inactive. No new compilation or invalid-bounds errors appeared in the final motion capture log. Existing unrelated Unity editor NullReferenceException persists; a clean full-game console or live verification of the user's exact colored-artifact reproduction is not claimed. No automated tests were added or run.

**Latest pacing correction:** source/asset inspection confirms Main once again uses PlayIntroHandoff's saved slowdown. The Player prefab remains 0.08× / 0.5 real seconds; its settings were not edited. The live artist preview now includes that same handoff presentation, while the isolated visual scrubber remains at normal logic speed. The narrow timing authoring command saved hold/column/light/flash/burst/wind/residual values 18/54/36/10/90/108/118. Regenerated phase 55 and 90 captures were inspected: the large burst remains strong around 0.9 seconds and visibly fades around 1.5 seconds instead of disappearing early. No damage, startup, recovery, Heavy tier, camera or saved height was reset. Compilation and visual rendering checks were used; no automated tests were added/run.

**Latest thunderstrike visual validation:** Unity completed compilation, the shader rendered through the isolated Unity camera, and regenerated phase captures were visually inspected after removing the dark halo join seams. Saved Main data confirms startup 2, captured-world endpoint 2, contact audio 3 and clip standing key 14. The saved height remains Y+3 and camera tuning 1.5 / 0.45. No automated tests or full prepared-team-swap regression run were performed for this visual revision; older live measurements below belong to earlier timing/camera revisions. Current visual evidence is `phase-001.png` (long strike entering above frame), `phase-003.png` (first contact), `phase-006.png` (glowing breakup), and `phase-014.png` (standing with a fading column). The updated preview reads the saved action timing dynamically.

No automated tests were added or run, per the user's explicit instruction. Compilation was requested through Unity and the new scripts compiled. Manual preview exercised the actual QTE state/motor, hidden flight, floor contact, particle emission and recovery attack continuation. Isolated phase captures show a burst larger than the actor and a visible later tail.

Interim Y+10 camera measurements: frame 7 body `(0.02,7.97)`, camera `(0.02,8.97)`, actor viewport `(0.50,0.50)`; frame 19 body grounded, camera Y 5.11; frame 70 camera Y 4.36. Orthographic size remained **6.844998** throughout. At frame 70 a normal attack entered ExecutionBreakdown and cleared the previous effect's particles. These are observations, not performance measurements.

Final **Y+5** manual check: frame 4 body `(0.46,4.78)`, hidden and physically airborne; camera `(0.46,5.78)`, actor viewport `(0.50,0.50)`. At frame 21 body `(0.46,-0.07)` was grounded and visible, with 36 sparks / 18 debris / 15 residual particles; camera Y 4.75. Camera size was **6.8** at both observations and on no-input completion. At frame 136 state was Idle, the actor visible, captured motion released and all effect particles cleared. Screenshots: `live-004.png` and `live-021.png`. Unity was returned to edit mode.

### Later camera-offset/exit correction — supersedes the framing observations above

Apricha reported excessive height and a jump cut on exit. Source inspection found the zoom-scaled floor minimum forcing the camera up to about floor+4.4 at size 6.8. Release immediately dropped the final correction and set the explore/combat cameras' PreviousStateIsValid false, discarding their damping history. The revised camera uses actor-relative height and retains its last rendered QTE view as the starting point for an eased return to live gameplay framing. It begins returning at contact, can survive early Execution Breakdown entry, stops on a newer handoff, and clears on extension disable. No MainCamera transform restore or stale preferred-camera snapshot is introduced.

Compilation completed with zero compile errors after this correction. Live observations: frame 7 body Y 3.97, camera Y 5.47; frame 14 grounded body Y -0.07, camera Y 1.43; lens remained 6.8. A later normal gameplay sample was around camera Y 3.35. These observations establish the lowered framing and valid normal view; the editor state changed during the follow-up inspection, so an uninterrupted early Execution Breakdown return capture is **not** claimed. Human should review continuous exit feel in normal play. No automated tests were added or run.

The new values are tunable starting values: pass when the actor stays comfortably framed during fall/contact and contact/expiry/early follow-up have no one-frame position or lens discontinuity. If framing is high, reduce Camera Actor Offset Y; if the return is too abrupt, increase Camera Return Seconds; if it feels sluggish, decrease it. Camera pause/hitstop follows scaled time. This correction does not reset height, VFX or Heavy tuning. During the concurrent playtest the saved Main profile changed from Y+5 to **Y+3**; that external asset edit was observed and preserved rather than overwritten. The profile class's initial default remains Y+5.

The artist live-preview command deliberately bypasses QTE readiness/team handoff and is labeled that way. Separate manual teammate swaps/readiness were exercised, including a normal-swap fallback rather than a successful Main plunge handoff in the attempted timing/placement. A complete prepared-swap matrix, moving support/roof/gap validation, other room confiner shapes, both facings and interruption/death coverage are **not** claimed complete. Runtime console inspection also found `Parameter 'Hash 125937960' does not exist` from the existing PlayerAnimator.SafeResetTrigger → TriggerSkill input path, and a Sprite Tiling/Full Rect message from HitBoxController.Update. Neither was a compile error in the new scripts; both are recorded rather than claiming a clean full-game console. Those unrelated systems were not changed for this task.

Human review should compare scale and tail at actual gameplay zoom. If the impact obscures enemies too long, reduce the envelope alpha/branch count before reducing its radius. If the silhouette disappears too fast, lengthen burst rather than increasing sparks. If late residuals feel noisy, shorten residual frames before shrinking the landing accent. These are tuning directions, not assertions that subjective feel has passed.

See the [manual regression checklist](../../TestPlans/main-qte-lightning-plunge-test-plan.md). `phase-*.png` are visual-only phase captures; live captures may represent earlier revisions, so use the measurements and revision notes above rather than treating every old PNG as current evidence. `live-007.png` was recaptured with the latest actor-offset camera. Older `live-013.png` / `live-027.png` precede the camera/scale revision and are historical only.
