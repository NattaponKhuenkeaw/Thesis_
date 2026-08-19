---
slug: "edge-snap-dash-sequencing"
status: approved
source: manual
gdd_tags:
  - "combat-scope"
  - "platform-input"
owner: "implementation-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Architecture Plan: Edge Snap and Dash Sequencing

## Current Implementation

- `ResolveActionOutputContext` maps `MatchInput` only from the captured start context, so a ground-started action calls `SnapToGroundForActionOutput` even after genuine support loss.
- Ground-departure cause distinguishes lost support from authored upward motion, but all upward `GetContacts` hits still count as support even when only a collider sliver remains on an edge.
- Dash Recovery must permit action interrupts without returning grounded locomotion.
- `PlayerLandingState` does not accept Dash, while `CanStartGroundDash` rejects the landing timer.

## Target Architecture

- Keep `PlayerMovement` as the cause authority and expose one read-only predicate for genuine ground departure (`LostSupport` or `DropThrough`).
- Add a short downward support ray at `GroundC.x - 1` that vetoes lingering edge contacts for an already grounded player.
- Make `PlayerStateMachine.ResolveActionOutputContext` the single edge-aware projection point: only ground-started `MatchInput` converts to Air after genuine departure.
- Preserve explicit output contexts and authored-motion grounding.
- Keep phase policy declarative: Dash Recovery permits Attack, Skill, Ultimate, and Jump while its input gate remains locked; Cancel retains the full authored action set.
- Landing participates in normal dash routing, and Dash start clears the landing timer.

## Affected Systems

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/InterruptPolicy.cs`
- `Assets/Project/Scripts/Player/States/PlayerDashState.cs`
- `Assets/Project/Scripts/Player/States/PlayerLandingState.cs`
- `Assets/Project/Scripts/Animation/PlayerAnimator.cs`

## Data Flow

`CheckGround/DropDownRoutine` records departure cause -> x = -1 support ray vetoes lingering edge contact -> FSM projects `MatchInput` -> genuine departure resolves Air -> combat exit skips Ground snap -> Falling or Air-context successor owns control.

`Landing RouteDashInput` -> normal cooldown/context validation -> Dash entry clears landing timer -> Startup/Active keep Dash-owned -> Recovery permits actions but locks locomotion -> Cancel opens grounded locomotion and the full action window.

## Compatibility and Risk

- No serialized data, dependencies, build settings, or public asset schema changes.
- Existing explicit Ground output, dash cooldown, air-dash count, and action resolver tables remain intact.
- ADR-003 and the GDD must be amended because this manual direction supersedes the earlier first-Recovery Ground Dash checkpoint.
