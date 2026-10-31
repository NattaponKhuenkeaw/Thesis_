---
slug: background-overlay-flash
status: approved
source: manual
gdd_tags: [visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Flash presentation contract

The user's requested phases define the authorized behavior: transparent → fade in → peak hold → fade out → transparent. All durations use existing logic-frame authoring and unscaled runtime time. Zero fade-in cuts to peak, zero hold begins fade-out immediately, zero fade-out cuts to transparent, and all-zero durations leave the overlay transparent. Peak Alpha remains the opacity authority, preserving the existing color semantics.

No new tuning values are proposed. New fields default to zero to retain existing authored presentation. New flashes replace an active impact flash; the existing Ultimate sequence priority and visual lifetime remain unchanged.

Clarity: independently visible phase controls and truthful timeline duration. Response: no input/state changes or added locks. Satisfaction: flashes and camera shake can coexist with position control. Fit: existing GDD `@tag:visual-audio` impact presentation. Motivation: unchanged combat rewards.

Gameplay review: verify phase readability against enemy telegraphs, repeated flashes, action end, freeze/slow motion and camera movement. If a flash obscures threats, reduce Peak Alpha or authored hold/fade duration. Existing audio/hit confirmation remains untouched.
