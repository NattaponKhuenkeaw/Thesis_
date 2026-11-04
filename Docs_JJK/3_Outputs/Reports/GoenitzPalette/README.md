---
slug: goenitz-frenzy-palette
status: needs-human
source: manual
gdd_tags:
  - visual-audio
  - characters
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Palette Mapping Evidence

Draft design data for the user's 2026-11-04 red-outfit request. GDD context: `@tag:visual-audio` and `@tag:characters`; see `../../Specs/goenitz-frenzy-palette-design-spec.md` for provenance, limitations and visual review.

- `mapping.json`: 21 explicit sRGB source/target pairs, palette-slot evidence and three selective profiles.
- `mapping.csv`: the same entries for inspection, including source usage counts.
- `audit.json`: all 385 input filenames, SHA-256 hashes, palette families, per-file dimensions and color-collision checks.
- `build_mapping.py`: dependency-free PNG reader and reproducible audit/preview generator. Supports this input set's non-interlaced eight-bit indexed/RGB/RGBA PNGs; unsupported formats fail explicitly.
- `palette-preview.template.html`: interactive review fragment. Source pixel geometry and alpha come from the original PNGs; color replacement is deterministic code, without generated artwork.

From the repository root:

```powershell
python Docs_JJK/3_Outputs/Reports/GoenitzPalette/build_mapping.py
```

This regenerates JSON/CSV/audit and `palette-preview.html` in this directory. To select another preview destination, pass `--preview ABSOLUTE_PATH`. To inspect source palette frequencies, pass `--inspect` with optional reference PNG paths. Inputs under `Assets/` are read only.

Preview integration validation on 2026-11-04: headless Chrome passed 33 frame/profile combinations plus color inspection, alpha/geometry checks, and red-source pass-through. Visual review of idle source versus full-reference output completed. These are browser design-preview checks, not Unity shader tests.

Following the user's runtime-integration request, `unity-validation.json` records six passing Unity validation groups and zero failures, covering real GPU palette rendering, tint/alpha/pass-through, lifecycle restoration, the actual dash animation and actual afterimage spawning. Rerun with `Tools/Project/Validation/Goenitz Frenzy Palette`. See `../../TestPlans/goenitz-frenzy-palette-test-plan.md` for exact scope and gameplay review steps.
