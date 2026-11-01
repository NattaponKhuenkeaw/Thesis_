# DevLog: 2026-11-02 — Targetless actions and ability HUD feedback

## Goal

Handle Apricha's targetless QTE/Dimension Cut bug, charged dash HUD icon request and successful Skill/Ultimate/Dash white overlay follow-up. Task: `../Specs/targetless-qte-dimension-cut-task-card.md`; GDD: `@tag:combat`, `@tag:characters`, `@tag:visual-audio`. Workflow: `/fix-bug`.

## Changes

- QTE keeps its earned window and can prepare a safe intro relative to the outgoing actor after all targets are lost. Existing live-target preference and unsafe-placement rejection remain.
- Main's cached Dimension Cut no longer requires an enemy to activate. Optional target capture remains; absent targets use its authored forward motion fallback. Cost, timing and cooldown policy are unchanged.
- Dash HUD uses existing `UI_Dash_HaveStack 1.PNG` at one full meter box and restores its ordinary sprite after spending/below threshold. The optional Meter Ready Dash Icon is editable in the Animation Editor.
- Successful manual Skill/Ultimate commitment and committed Dash reuse the same white overlay as NA. Rejected inputs, QTE intros and automatic chains do not report manual success.
- Preserved pre-existing scene, character, prefab, UI binding and documentation edits. No commit, merge or publishing.

## Verification

- Unity asset refresh and compilation completed; Editor.log records a completed domain reload, no `error CS` entries, and runtime/editor output assemblies newer than final source edits.
- Scoped whitespace/diff check passed. Unrelated pre-existing whitespace findings were left alone.
- User explicitly requested no script tests. Removed this session's test-script edits; no automated tests ran. Existing test expectations were left unchanged.
- Unity MCP was initially reachable but intermittently failed readiness/console pings during reload. No live Game-view playthrough or screenshot verification is claimed.

## Human review

Play the earned QTE after killing the last enemy, perform cached cuts without enemies on ground/air, check wall/ground safety, watch the dash icon at the meter threshold and verify white overlays on committed actions versus rejected inputs. Final combat feel and visual approval remain with Apricha.
