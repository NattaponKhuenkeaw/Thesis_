---
slug: main-skill-potential
status: needs-human
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Skill Potential validation

Task: `../Specs/main-skill-potential-task-card.md`. Design sources: Apricha's 2026-11-02 request and GDD `@tag:combat`, `@tag:characters`.

## Automated evidence

Run `Tools/Project/Combat/Validate Main Skill Revision` in Edit mode. Uses the existing synchronous isolated-scene runner without saving/replacing the user's scene. Result: 68/68 pass in `Temp/main-skill-revision-tests.xml`. Includes five Potential levels, exact 20%/40% vulnerability replacement and expiry, ally source-ATK snapshots and refresh, receiver snapshots, Magnetism during damage cooldown/after quota, inward movement without overshoot/lift, terrain, immunity, freeze/grab ownership, expiry and reset including already queued pull.

The broader Main Potential suite has 11 remaining meter/cache/target mismatches, detailed in the task card. These are not waived as passing.

## Human combat review

1. New player/readability: inspect Base and each upgraded Skill panel. Base has no vulnerability; Lv1 has 20%/17s and +5 AE Reward; Lv2 adds Magnetism; Lv3 replaces 20% with 40%; Lv4 adds the ATK-based DM/ADI buff. Confirm the existing field visual explains the pull area and the debuff marker expires.
2. Response/feel: cast Lv2+ beside enemies on both sides of the field and during a combo. Starting speed is 1.5 world units/second on the upgraded Skill projectile. Pull should be gentle and visible between damage ticks; it must not stun/grab enemies, lift grounded enemies, or lock player input. Increase speed if imperceptible; decrease if enemies cannot contest the field.
3. Skill/team rotation: at 80 effective ATK, successful Lv4 Skill hits grant +10 percentage points of DM/ADI to Main and off-field allies. Increasing effective ATK to 164 leaves the previous snapshot intact until another hit; reapplication gives +20.5 and refreshes to 17 seconds without stacking. Switch allies and confirm the same bonus; wait 17 seconds after the final hit for expiry.
4. Stress/abuse: repeat casts, hit crowds, overlap fields, and change targets. Strongest pull wins rather than accumulating speeds. Check terrain walls, armored/immune enemies, grabbed enemies, hit freeze and flight enemies. Expire/pool the projectile and confirm pull stops.
5. Regression: check the user's current Shadow Meter, Dimension Cut, QTE and Void Area behavior without altering their authored values to match older assertions.

Final game-feel/readability approval remains human-owned.
