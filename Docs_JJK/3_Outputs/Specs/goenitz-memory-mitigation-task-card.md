---
slug: goenitz-memory-mitigation
status: implemented
source: manual
gdd_tags: [characters, combat, progression]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Goenitz Memory Mitigation

The existing Reaper kit gains six Memory ranks per track. Held inputs, cancel windows, AE drain, projectile variants, and the pre-existing EX crit rewards use their current implementations. The authoring lives in Goenitz's character, action, projectile, and recipe assets.

Yonokaze splashes 250% of the triggering confirmed critical damage within 10 units once per cast using Rei's yellow explosion. EX uses a red explosion with a 3-unit radius and 20% of that tick's confirmed damage as Additional Bleed, with a one-second cooldown shared across active EX tornados. Both reach other enemies. The two variants share a successful duplicate-spawn cooldown and roll once per cast. Yamidokoku's +250% bonus means 3.5 times the normally resolved critical damage.

Blood Frenzy and its one-time room burst resolve when the held Ultimate is accepted, before its animation. Frenzy ends through the existing AE and Shin paths. The native Bleed uses the existing tick rate and can finish its normal duration after Frenzy ends. Sharing and evasion follow the owner's Frenzy lifetime. A qualifying kill extends that same gauge.

Playtest in a populated room. Check held-input response during combat, whether the two splash radii are readable against the original crit effects, and how simultaneous tornados read in a crowd. Swap away during Frenzy and check expiry, then use Shin and verify that a new room receives Bleed only while Frenzy is active.
