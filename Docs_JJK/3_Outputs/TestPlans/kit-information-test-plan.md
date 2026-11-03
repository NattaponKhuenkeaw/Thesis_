---
slug: kit-information
status: approved
source: manual
gdd_tags: [ui, characters, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Kit Information Validation

Native command: `Tools/Project/Validation/Run Kit Information Checks`.
Evidence: `../Reports/KitInformation/native-checks.json` and rendered PNGs in the same directory.

## Automated Coverage

- Independent tap/hold Ultimate descriptions and typed scalar bindings.
- Cross-track changes, dedicated cooldown modifiers, operation ordering, and energy/cooldown clamps.
- Positive/negative unknown runtime requirements remain conditional; explicit preview state evaluates them.
- Step-only bonuses do not inflate whole-action values.
- Targeted patches respect progression and leave other actions unchanged.
- Conflicting replacements, bad bindings, broken references, and duplicate IDs are diagnosed.
- QTE reuses an ordinary action with separate presentation identity.
- Standalone display sections are independent of combat categories.
- Permanent replacement/lock state and tier-zero base actions.
- Read-only inspection and repeatable presentation migration.
- Runtime controller fixed-result parity checks.
- All authored hero assets at each Potential tier.
- Nested card layout at 1280x720 and 1024x768, positive text widths, fitted metadata/descriptions, and stale selection clearing.

## Manual Follow-up

- Open the inspector in Menu and Training, switch characters, and scroll through Main's two Ultimates, QTE, Special, and Passive.
- Change Training Potential tiers and confirm displayed values/patches refresh without changing saved progression.
- Exercise existing tap/hold Ultimate, QTE swap, and cached Dimension Cut gameplay.
- Review secondary-action wording and existing effect-specific narrative numbers as part of character content tuning.

## Results

20 native checks passed, 0 failed. All nine hero definitions were inspected at tiers 0 through 4. Runtime compilation completed without C# errors. Wide/4:3 renders were inspected, including the bottom of the scroll and long Passive copy. `asset-migration-audit.json` verifies preservation of existing payload lines, apart from missing IDs and the equivalent CameraGirl QTE name-to-ID reference migration. No manual gameplay playtest is claimed.

## Potential/Header Revision Coverage

- Main's Dimension Slash is absent at Passive 0–3 and present at Passive 4.
- Roots report the actual supplied progression level, including tracks without authored modifier nodes at every tier; children do not repeat it.
- Temporary runtime readiness does not remove learned actions.
- Direct gameplay Potential conditions and optional effect-granted presentation minimums both filter entries.
- Rugal EX is shown at Passive 4; CameraGirl's Shadow Assist at Ultimate 1.
- Explicitly authored Normal Attack entries are excluded, while QTE reuse and Normal-input standalone actions remain.
- Conditional and step-scoped modifiers do not add generated `Conditional upgrade` prose; implicit QTE, Ultimate, target, and resource activation phrases are omitted. Explicitly authored activation copy may still appear.

## Regression Follow-up: Main Shadow Meter

- In Unity, hit an enemy with Main's Ground Normal Attack and confirm the normal hit meter gain still follows `Attack3`, `Attack6`, and fallback gain rules.
- Hit with Main's Air Normal Attack and confirm its step/fallback gains still resolve.
- Confirm stable combo action IDs in the resolver table match the action keys stored in `combatRules.meterGainRules`.
- This scenario is documented but was not playtested in this workspace pass.
- Ground/Airborne/Both, modified cooldown, and AE are structured header tags with code-drawn symbols.
- Updated native previews verify Lv.0 and Lv.4 kits, symbol rendering, badge widths, title/body fit, and root labels at 1280x720 and 1024x768.

Current previews: `kit-main-lv0.png`, `kit-main-lv4.png`, `kit-main-1024.png`, `kit-main-middle.png`, and `kit-main-bottom.png` in `../Reports/KitInformation/`.
