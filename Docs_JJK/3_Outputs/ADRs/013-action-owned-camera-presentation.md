---
slug: main-dimension-slash
status: approved
source: manual
gdd_tags: [visual-audio, combat, architecture]
owner: architect-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# ADR-013: Action-Owned Camera Presentation

## Approved extension — 2026-10-29

### Single zoom duration clarification — 2026-10-29

After reporting that a 1000-frame hold was cut short and selecting the single-zoom option, the user clarified: “One zoom per event, the hold mean a Zoom Hold Freeze(at peak zoom) and the return duration is the cam return to it original scale.” Single-zoom events now own a scaled-gameplay-time envelope measured at LOGIC_FPS, independent of step length. Natural action completion and auto-chain steps allow that envelope to finish; interruption of its owning combat state releases it. Field-session invalidation, death, hitstun, room change, higher-priority focus/Ultimate, disabling the manager, and a replacement camera event cancel it. Pause and hitstop stop the clock. This supersedes the action-end release and action-frame-only clock contract for single-zoom events. Legacy unconverted keyframe tracks retain their previous step-owned contract.

The editor presents one target zoom, transition-in frames, peak-hold frames and return-to-normal frames, plus optional lock and horizontal offset. Opening a legacy ActionCameraMotion event converts its authoring settings to this envelope using its strongest zoom-in key and that key's frame as the transition duration, retaining explicit hold/return values. Original keys remain serialized for recovery; the single-zoom settings become authoritative. The return blends to the camera system's current base lens, avoiding a stale cached lens that could overwrite newer room framing.

User approved Screen Impulse Camera Control with “lgtm” after reviewing `../Specs/screen-impulse-camera-control-task-card.md`. Screen Impulse may opt into action-frame zoom-in/out and a captured rendered-world-position lock, with transition, hold and return frames. Reuse generation-scoped action-camera ownership and the existing bounded Cinemachine extension. Screen Impulse zoom-out is limited to the supported room rectangle; existing ActionCameraMotion limits remain unchanged. The lock cuts immediately, the lens follows its authored transition, and return blends toward current base composition. Finish/interruption releases ownership. This approval extends the original restrained lens/additive-only contract for this opt-in event; it does not change existing action assets.

| Field | Value |
| --- | --- |
| Status | **Accepted — user “LGTM, arch approval”,2026-10-27** |
| Date | 2026-10-27 |
| Author | architect-agent |
| GDD Section | `@tag:visual-audio`, `@tag:combat`, `@tag:architecture` |

## Context

The approved Main Dimension Slash direction needs a restrained framing push and a distinct finishing recoil synchronized with its authored action. The existing ActionResolver/CombatPlayerState owns action timing; CameraManager owns room framing and Cinemachine cameras. Current ScreenImpulse requests expose scalar strength rather than a directional framing track; the Ultimate overlay's zoom switches camera priorities and is not an independent standalone Skill lens track.

The user selected Passive Lv.4, five Dimension Cuts, and a timed Skill-command follow-up. That gate can reuse a separate SkillAction and existing passive counter/tag/window data. Camera presentation should not require replacing those authorities or introducing another runtime sequencing system.

## Proposed Decision

Add a narrowly scoped, data-authored action-camera presentation event/track consumed by the existing combat executor. CombatPlayerState drives progress from the same logical action frame as actor/VFX/audio/contact tracks. CameraManager owns a generation-scoped claim for that presentation and applies a bounded additive correction through a cached CinemachineExtension. No action writes MainCamera transforms, restores a cached global camera position, changes vcam priorities, or starts an independent Timeline/director clock.

The camera layer owns only its temporary orthographic-size reduction and positional correction. It does not own player movement, global time, target selection, room transitions, base camera damping, or QTE framing. A completed/canceled/disabled/invalidated owner releases its claim; a stale generation cannot modify or release a newer owner's claim. Ordinary hitstop holds action-frame progress, while existing confirmed-impact vibration can run on its established clock. No full-burst global freeze is introduced.

At the current project's orthographic, untilted BoxCollider2D room bounds, clamp the **final rendered screen rectangle** after applying the correction. The installed CinemachineConfiner2D confines at its Body stage; a Finalize correction would otherwise bypass that protection. Do not claim an axis-aligned Box bounds approximation is safe for arbitrary polygonal/rotated rooms. Suppress positional presentation when supported bounds/camera assumptions are absent; retain base camera ownership and verify degradation explicitly.

The initial camera profile uses translation and a restrained lens push, with no camera rotation. Numeric settings remain Starting values in the architecture/test packet. It should return to zero correction by natural completion and release immediately on an accepted interruption. This is an action-presentation extension, not a replacement camera system.

## Alternatives Considered

| Option | Benefit | Limitation |
| --- | --- | --- |
| Existing scalar shake only | No camera extension | Cannot express the requested deliberate framing/recoil path; appropriate as fallback, insufficient as the final camera-motion plan |
| Reuse Ultimate overlay/zoom | Existing blend and cleanup | Couples the new Skill to an unrelated cinematic camera switch/overlay and does not expose a frame-authored envelope |
| Animate MainCamera transforms or restore cached positions | Direct access | Competes with Cinemachine, room framing, QTE handoffs and camera blends; cached restores can overwrite a newer owner |
| New Timeline/custom ability director | Rich independent sequencing | Adds a second action clock and duplicates resolver lifetime/interrupt semantics |
| Scoped additive Cinemachine extension | Preserves existing camera and action authorities, explicit cleanup | Adds a small reviewed integration surface; bounds and handoff tests are essential |

## Consequences

### Positive

- Actor, effects, sound, contacts and camera can be aligned using the same authored action frame.
- Existing exploration/combat framing and gameplay state remain authoritative.
- Scoped ownership prevents late cleanup from corrupting another action or QTE transition.
- No new package, camera asset, shader/render feature or saved progression format is required.

### Trade-offs

- Requires explicit action-event data/editor support, executor lifecycle calls, CameraManager claim methods and one focused extension.
- Pixel-art zoom needs captured-gameplay review; component availability alone does not prove crisp presentation.
- Until general bounds support is implemented and approved, positional correction is deliberately limited to supported camera/room geometry. Tests must report unsupported cases rather than silently asserting safety.
- An accepted cancel may truncate action-owned residual VFX; independent post-exit VFX tails are not introduced by this ADR.

### Migration

New event enum values must be appended without renumbering existing serialized values; default/absent presentation data performs no correction. Existing actions and camera behavior remain on their current paths. Apply narrow edits to the dirty working tree and verify existing QTE, Shadow Meter, ordinary Skill and Ultimate behavior.

The separate passive filter addition is opt-in provenance metadata, not a new passive manager: pass the actual CombatExecutionContext at action start and let only the new counter rules require a non-QTE on-field manual start. Existing filters retain their default behavior; do not globally suppress QTE passive callbacks.

## Related

- [Architecture plan](../Specs/main-dimension-slash-arch-plan.md).
- [Approved design](../Specs/main-dimension-slash-design-spec.md).
- [State-owned movement ADR](003-deterministic-player-state-owned-movement.md).
- [Runtime effect recipes ADR](002-runtime-effect-recipes.md).
- [QA plan](../TestPlans/main-dimension-slash-test-plan.md).
- Existing implementation examples: `CameraManager.cs`, `QteIntroCameraImpact.cs`, `CombatPlayerState.cs`, `ActionEventFrameData.cs`.

Accepted by the user on 2026-10-27: “LGTM, arch approval.” Implementation uses the appended camera event and token-owned final Cinemachine contribution. Captured gameplay and remaining review limits are recorded in the Dimension Slash implementation report.
