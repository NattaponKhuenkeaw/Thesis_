---
slug: rei-memory-routing
status: needs-human
source: manual
gdd_tags: [characters, progression, ui]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Rei Memory routing

Workflow `/fix-bug`. Rei's authored upgrades are present, but the shared track only reads descriptions and combat ranks for Main. Rei also exposes the legacy Potential page through Previous Upgrades.

Use a kit asset setting to opt Main and Rei into the same Memory rank and base stat rules. Use the authored node descriptions in Memory map previews and Hard reward cards. Remove the legacy page entry points for Rei in the inspector and Training. Keep saved fragment ranks available as historical data, following Main's existing migration behavior. Verify existing Memory save data and newly earned steps both unlock Rei's combat upgrades, and retain legacy progression for unmigrated kits.

The isolated Unity EditMode run passed all 63 cases, including Rei's saved progress, every major description, the generic stat description, and combat effects driven by earned Memory ranks. The authored inspector route has a PlayMode regression for switching from a legacy character to Rei. That interactive case remains to be run.
