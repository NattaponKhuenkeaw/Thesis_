---
slug: fullscreen-execution-slash
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Clean fullscreen Execution Breakdown slash

Source: Apricha's 2026-10-29 direction: remove grain/dissolve/noise and the fullscreen mask entirely; use the same transparent purple glow slash as Dimension Slash, a glowing white background, a much stronger existing shockwave, and a simple fast slash routine. Workflow: `/fix-bug`. This explicit request authorizes presentation replacement within the existing Action Event / ProceduralSlashField / background overlay / cut-refraction systems. GDD `@tag:visual-audio` calls for large readable attack shapes and impact flashes.

## Implementation

The saved Execution Breakdown event remains at its authored trigger frame 136. It now references the shared DimensionSlashRibbon material, builds one deterministic camera-spanning ribbon, and uses the same ribbon shader inputs for transparency, lavender core, purple glow gradient, traveling reveal and shrinking fade. Deleted the separate ExecutionBreakdownMask shader/material and removed the mask geometry/sampling path. Former serialized names remain only as migration attributes, preserving older stored settings.

The event invokes the existing UltimateFlashOverlay impact flash behind gameplay. Its existing shockwave/refraction follows the actual ribbon endpoints, revealed segment, width and fade. Increased the editable refraction ceiling from 16 to 64 reference pixels; existing effects retain their saved strengths. No noise/grain/dissolve routine was added, and the shared Dimension Slash shader is unchanged.

Starting values for this visual revision: one-frame reveal, one-frame hold, eight-frame decay and twelve-frame total effect; eight-frame white flash; ribbon width 1.2, existing angle 24.2 degrees and viewport coverage 1.15; ten-frame shockwave with width 0.18 screen heights, 48 reference pixels of refraction, zero chromatic split and two reference pixels of directional blur. The owner's “1000 times impact” is treated as a request for a substantially stronger perceived hit, not an unbounded distortion multiplier. All controls remain authored in the Action Event.

Actor animation files, hitboxes, damage, current action timing, other camera events and sound entries were not edited for this revision. The authoring utility's future defaults now reproduce the replacement; it was not executed.

## Experience and manual review

Clarity: one slash direction and a short white background cue. Satisfaction: bright purple core/gradient plus strong cut-following refraction, with the existing authored impact audio. Response: the visual runs on the existing action clock; cleanup still belongs to the current action. No new gameplay states or resource rules.

At gameplay zoom, confirm a single clean stroke, a readable purple edge over the white background and a clearly visible wave without a lingering screen tear. If weak, raise refraction within the existing slider limit or ribbon width; if the world becomes unreadable, reduce refraction first. Check both facings, pause/hitstop, interruption and repeated use for correct mirroring and cleanup. Verify the current actor animations and hitbox data remain as authored.

Validation is static file review, reference searches and scoped whitespace checks only. No Unity scripts, authoring commands, compiler commands or tests were run, respecting Apricha's standing restriction. Runtime appearance remains for human review.
