---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags:
  - visual-audio
  - combat
  - inspirations
owner: game-design-agent
human_checkpoint: required
next_agent: game-design-agent
blocked_by: []
---

# Dimension Slash: Primary-Source Research

Research date: 2026-10-27. This note supports the design spec; it is not architecture approval or evidence that any behavior has been implemented.

## Evidence Boundaries

- **Local reference observations** belong to the clip analysis in the design spec. This researcher did not analyze the video or its audio.
- **Published technique** means the linked developer talk or official documentation directly supports the claim.
- **Design inference** means a proposed application to Neo-JJK, not a statement about BBEEx internals.
- No primary source establishing BBEEx's internal hitstop implementation, camera algorithm, render pipeline, asset production process, or exact action timing was inspected. Do not present any of those as known facts.
- The GDD explicitly supports economical actor animation, strong silhouettes, large slash shapes, impact flashes, hitstop, camera shake, and distinct skill/ultimate charge and release audio (`GDD_Neo-JJK.md`, `@tag:visual-audio`). BBEEx is explicitly listed under `@tag:inspirations`.

## Research Findings and Application

| Topic | What the primary source supports | Proposed application to this action (design inference) |
| --- | --- | --- |
| Impact emphasis through stopping | Masahiro Sakurai explains hitstop as emphasis for important moments. The official video's description additionally recommends keeping slow motion brief and returning to normal speed for more impact. [Stop for Big Moments!](https://www.youtube.com/watch?v=OdVkEOzdCPw) | Reserve the most pronounced temporal punctuation for the Dimension Cut. Let the barrage retain flow, then create a perceptible held beat before release. Compare that version against continuous slow motion in the playtest. |
| Strong animation with limited frames | Skullgirls lead animator Mariel Cartwright's GDC session addresses strong keys, anticipation, and timing under fighting-game frame restrictions. [Making Fluid and Powerful Animations For Skullgirls](https://www.youtube.com/watch?v=Mw0h9WmBlsw) | Spend actor production effort on readable wind-up, release, and finish silhouettes. Keep the actor visible at important beats; slash VFX carry the repeated burst shapes. More in-between drawings are not the first tuning lever. |
| Shake envelope and hierarchy | Squirrel Eiserloh's GDC slides describe bounded trauma that decays over time, nonlinear conversion of trauma to shake, smoother noise instead of independent random samples, and additive shake that preserves the base camera. See PDF slides 10-18. [Juicing Your Cameras With Math](https://media.gdcvault.com/gdc2016/Presentations/Eiserloh_Squirrel_JuicingYourCameras.pdf) | Use an impact hierarchy: restrained burst kicks and one larger finishing kick with a clear decay. Preserve the room's framing and clamp rules. Do not accumulate a new permanent camera offset on every hit. |
| Events can drive camera impulses | Cinemachine's official Impulse Source documentation supports generating camera impulses from game logic, with direction/strength and predefined or custom signal shapes. [Cinemachine Impulse Source](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineImpulseSource.html) | If the project's inspected feedback path already routes authored action events to an impulse, reuse it. A directionally biased finishing recoil can be authored as a short signal. This source is not evidence that Main currently exposes those controls. |
| Kick and secondary vibration are separable | Official Impulse Listener documentation describes applying the incoming impulse plus an optional secondary reaction. It also documents 2D-distance handling and gain. [Cinemachine Impulse Listener](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineImpulseListener.html) | Treat the first camera kick as the directional impact and its tail as vibration. Avoid a full-strength rumble throughout the barrage. Validate installed-package support before using public-documentation inspector fields. |
| Crisp sprite import and scale | Unity's URP pixel-camera preparation guidance specifies matched Pixels Per Unit, Point filtering, no compression, pixel-snapped pivots, and reference resolution matched to the intended art. [Prepare sprites for the pixel perfect camera](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/2d-pixelperfect-prep-sprites.html) | Match New_sprite's inspected grid and PPU. Author slash edge clusters at the intended displayed size. Preview at the actual game view before approving; simply downsampling a soft effect does not establish consistent pixel structure. |
| Cinematic camera movement has a pixel tradeoff | Cinemachine states that blending cameras temporarily ceases to be pixel-perfect; the extension chooses a compatible orthographic size. It also lists incompatibilities that must be checked against the scene. [Cinemachine Pixel Perfect](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePixelPerfect.html) | Do not promise both unrestricted smooth zoom and strict pixel perfection. First test the current camera with a restrained positional push. Evaluate any zoom at the actual output scale and explicitly choose the acceptable tradeoff. |
| Actor and effect layers can be exported separately | Aseprite supports sprite-sheet exports filtered by visible layer or selected layer and tagged frame ranges. [Sprite sheets](https://www.aseprite.org/docs/sprite-sheet/) | Keep editable actor, slash core, slash edge, debris, and accent layers separate. Export actor and effects independently so camera and VFX timing can be tuned without repainting the actor. |
| Repeatable export is available | Aseprite's official CLI supports batch exports, selected layers, tags, PNG sheets, and accompanying JSON. [Command line interface](https://www.aseprite.org/docs/cli/) | If an installed executable is available, use repeatable exports; retain editable sources. The runtime can continue using imported PNG sprites and existing animation/VFX assets. |

## Recommended Production Toolchain

**Primary recommendation:** an installed pixel-animation editor (prefer Aseprite if available) for deliberate actor keys and slash flipbooks, plus Unity Sprite Editor, Animation preview, the existing VFX library/pooling path, and ActionResolver-authored action events. Use Unity MCP for inspecting assets, configuring approved data, verifying console state, and capturing the result. This keeps the action's runtime choreography inside the project's existing action system.

Evidence from `Packages/manifest.json` during this read-only pass:

| Existing package | Version inspected | Implication |
| --- | --- | --- |
| `com.unity.2d.aseprite` | 3.0.1 | An Aseprite importer is already declared; no installation is proposed. Direct `.aseprite` import is optional until its project workflow is verified. |
| `com.unity.2d.sprite` | 1.0.0 | Existing sprite authoring/import workflow. |
| `com.unity.cinemachine` | 3.1.6 | Reuse the existing camera system; verify capabilities in this installed version. |
| `com.unity.render-pipelines.universal` | 17.3.0 | Pixel-camera recommendations must use the URP workflow. |
| `com.unity.timeline` | 1.8.10 | Available for an offline storyboard if useful; do not add a second runtime clock simply because Timeline is installed. |

`Get-Command aseprite,aseprite.exe` found no executable on PATH. This does not prove it is absent from the machine. No installation, purchase, dependency change, camera-component change, or pipeline change is authorized by this note.

The current manifest does not directly declare VFX Graph or Shader Graph as standalone dependencies. The project stack's broad tools list is not evidence of an available or appropriate VFX Graph workflow. Favor verified sprite-based capability for the main graphic shapes; optional procedural particles are secondary accents subject to the system audit.

### Why this route fits

- Deterministic sprite flipbooks give direct control over silhouette, edge thickness, hold frames, and palette, supporting the requested pixel style.
- Separate actor and effect exports let a slash briefly occupy a much larger area than the body without hiding the actor for the entire sequence.
- Existing authored action events can remain the proposed choreography authority; the system audit determines which event fields and clocks actually work.
- A studio-scale 3D simulation or new VFX package is unnecessary for proving the requested impact and rhythm. Any later need should arise from a failed prototype, with explicit approval for dependency changes.

## Proposed Tuning Order

These are design hypotheses to test, not source-backed BBEEx implementation details:

1. **Silhouette and spatial story:** Can an observer identify the actor, the barrage's target region, and the Dimension Cut direction with camera shake disabled?
2. **Rhythm contrast:** Are the bursts perceived as deliberate groups, and does a held pre-cut beat make the finisher read as a separate climax?
3. **Impact alignment:** Do VFX contact, authored damage, camera kick, and audio onset agree at the same intended beat?
4. **Time punctuation:** Does the barrage remain fluid when only selected beats receive hitstop? Does the finisher gain weight without delaying return to control?
5. **Camera envelope:** Is the finish stronger with a short directional kick and fading tail than with continuous high-amplitude shake? Do room edges remain acceptable?
6. **Pixel finish:** Does camera motion preserve a coherent pixel scale and crisp edges at the real game output resolution?
7. **Audio finish:** Can the player recognize burst groups and the finishing release by sound alone? Is the finish audibly distinct in a crowded encounter?

No numeric gameplay tuning values are prescribed by these sources for this project. Any timing, shake magnitude, pixel displacement, frame hold, or hit count introduced in the design spec must be labeled **Starting value**, with its own micro test, pass/fail metric, and adjustment direction.

## Source Access and Limits

- The official Sakurai and GDC YouTube descriptions and the official GDC session summaries were accessible through search; full video transcripts were not obtained. Claims above stay within those descriptions. No detailed reconstruction of all of Sakurai's hitstop techniques is claimed.
- [Eight Hit Stop Techniques](https://www.youtube.com/watch?v=tycbMSjDDLg) is a relevant official further-viewing reference, but its complete contents were not accessible in this pass and are not used as evidence for specific mechanics here.
- Eiserloh's PDF was inspected directly; it notes that its animated examples do not animate in the PDF.
- GDC Vault's animation-session page redirected to a maintenance page when opened. The official GDC YouTube entry and cached official GDC session description supplied the summary claims above.
- Public Cinemachine `@3.1` documentation currently identifies itself as 3.1.7, while this project declares 3.1.6. Newer fields in public docs are not established project capability. Inspect the local installed package before planning their use.
- Source citations support general techniques and official tool behavior. The proposed combinations and production choices are this design's inferences.
