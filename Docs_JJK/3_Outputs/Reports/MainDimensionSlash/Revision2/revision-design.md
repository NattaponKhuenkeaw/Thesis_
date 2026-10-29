---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Procedural Slash Revision — 2026-10-28

The user tested the first version and rejected its smoothness/reference likeness. Their new direct instructions supersede the old six-burst sprite choreography: independently revealed purple glowing cuts, varying size/length/timing/direction, then contracting width/height; actor anticipation/lunge, disappearance during the barrage, reappearance/plunge, and reuse of the existing Dimension Cut sprite/action; about20 Low-impact contacts without screen impulse. A follow-up typo was corrected: **keep critical hits enabled**; do not add a new per-hit crit policy.

## Reference Study

Reviewed all seven supplied images and the previously timestamped original video analysis. Images2–7 show broad diagonal/near-vertical ribbons intersecting persistent thin seams. One broad shape becomes a narrow line while later cuts are already growing. Brightness concentrates along the revealed edge. This overlap and asymmetric lifetime are missing from the first implementation's four-frame repeating sprite swaps. Image1 supports the judgment-cut lattice direction, not a requirement to reproduce blue color; the requested game theme is purple.

Observation does not reveal BBEEx's engine implementation. Procedural shader/mesh rendering is our implementation choice, not a claim about their internal technology. Particle System could emit independent quads, but this action needs exact timeline scrubbing, deterministic replay, and actor-visibility cleanup. A bounded single mesh with independently timed shader ribbons fits the existing action-event path and avoids a new global VFX manager or particle simulation clock. Unity documents arbitrary mesh UV channels and renderer property blocks; the implementation carries each ribbon's timing/shape in mesh UVs and changes one shared frame parameter. Property blocks are not asserted to preserve SRP Batcher compatibility or guarantee performance.

Sources: [Unity Mesh.SetUVs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetUVs.html), [Mesh.SetVertices](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetVertices.html), [MaterialPropertyBlock](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/materialpropertyblock), [ParticleSystem.MainModule](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/particlesystem/mainmodule).

## Concrete Revision

Starting tuning values, to be tested in captured gameplay:

- Existing Passive4/five-paid-cut/Skill opportunity gate remains.
- Preparation node:104 logic frames, anticipation0–8, short lunge9–18, hidden actor18–89, return above the field90, plunge94–103. The single Recovery/Cancel boundary104 structurally auto-chains into the final node.
- Nineteen two-frame Primary hit windows at24,27,...78, Low tier,58% combined damage budget. No new invulnerability. Existing action multiplier5 is retained pending balance review.
- Procedural field starts18:19 independent broad ribbons plus12 thin supporting seams. After the user's scale correction, length/spread fit almost the full current orthographic viewport (starting coverage1.15x viewport diagonal), variable widths0.1–1.1world units/reveal4–9frames/decay16–27frames, varied angles/positions/reverse directions. A private per-execution seed preserves global gameplay RNG and permits fixed-seed preview.
- The user's additional layer instruction splits the31 ribbons into25 normal and6 foreground ribbons (approximately20%), distributed through the sequence. Normal sorting matches the existing generic VFX prefab: `WorldUI`, order999. The foreground pass uses that existing sorting layer at order12000, above the current order9999 world foreground/ground presentation. Both layer names/orders and foreground share are editable in the action event. No global SortingLayer or build configuration change is needed. Screen-space overlay UI remains on its own Canvas path.
- Each ribbon reveals from one end via an alpha mask with a moving luminous head; after a brief hold its thickness and length contract and opacity falls. Purple additive rim/glow remains around a pale lavender pixel core. Spatial edge quantization preserves the pixel direction while frame interpolation remains continuous. No post-processing or bloom configuration change is required.
- The field is sampled from the action clock plus bounded scaled render interpolation, so pause/hitstop do not advance it. The captured field origin stays fixed as the actor later plunges. Exit, node replacement, death/field invalidation and disable restore the actor's previous renderer visibility and stop the field.
- Final node reuses the current Dimension Cut's actual clip, timing and copied MotionData, with one42%-budget Low hit and purple procedural finishing ribbon. Reappearance uses `main_Jump.anim`, verified to reference `New_sprite/Ch1_Jump.PNG`; an old placeholder plunge-charge source discovered in the first capture was rejected. The final stays part of the same SkillAction, so it does not spend another cached cut or advance the five-cut gate.
- Per-hit `suppressImpactFeedback` disables global hitstop/critical minimum and thus the existing hit-triggered camera impulse for these contacts. It does **not** suppress critical damage or enemy damage reactions. Generic contact VFX is separately suppressed to keep the field readable. Existing attacks retain default behavior.
- No authored camera impulse or zoom event is used in this revision. The final shape and motion supply the impact.

## Validation and Tuning

Clarity: first/next cut edges remain identifiable; thin tails must not become opaque fog. Response: eligibility/input routing remains unchanged, actor disappearance introduces no invulnerability. Satisfaction: reveal/head glow/overlap and action audio carry the barrage; critical hits must not insert global holds. Fit: purple effects, stepped core edges and current Main/New_sprite identity. Motivation: current Potential-earned follow-up remains intact.

Pass conditions:20 accepted Primary contacts against a durable target (19+1), no extra cached cost, critical damage still occurs, timeScale never drops due to these hits, no action-owned screen impulse, actor hidden only inside the authored interval and restored after cancellation/disable, actual final animation/motion matches Dimension Cut. Independent slash size/angle/timing and slide→head glow→collapse must be visible in stepped and normal-speed capture. If the field obscures the enemy, reduce concurrent thick tails/width/spread before reducing the reveal continuity. If motion looks stepped, check actual render FPS and interpolation before adding sprite frames. Human feel/art review remains required after the concrete preview.

Workflow: `/fix-bug` for the reviewed first implementation. Existing ADR-013's camera authority remains; this revision removes that action's camera envelope and appends a local procedural action event. No new dependency, global manager, renderer feature, build/CI setting or team publishing is introduced. Existing dirty changes are preserved on `codex/dimension-slash-procedural`.
