---
slug: "artifact-build-system"
type: design-spec
status: approved
owner: game-design-agent
gdd_tags:
  - "progression"
next_agent: architect-agent
---

# Design Spec: Complete Artifact Build System

> **Produced by:** 🎮 Game Design Agent
> **Task Card:** `artifact-build-system-task-card.md`
> **GDD Source:** `@tag:progression` — Artifact System (Passive power-ups, Soul Shop loadout settings)
> **Date:** 2026-06-05

---

## 1. Player Experience Description

### What the Player Sees

The player opens the **Team Builder** panel (Soul Shop / Lobby menu). Below the 3 team character slots, they see a toggleable panel area. When the **Artifacts tab** is active, the panel switches from the character grid to an **Artifact Grid Panel** — a scrollable grid of all owned artifacts displayed as small cards with icons, names, and rarity-colored borders.

Each of the 3 team character slots already shows **3 small artifact sub-slots** beneath the character portrait. These sub-slots are either empty (dark outline, muted icon) or filled (glowing border, artifact icon visible).

### What the Player Does

1. **Browse**: The player scans their artifact collection in the grid panel. Each card shows the artifact icon and a brief stat hint.
2. **Preview**: The player **clicks** any artifact card in the grid. An **Artifact Preview Panel** slides in from the side, showing the artifact's full details — icon, name, stat bonuses, passive description, and lore text.
3. **Equip**: The player **drags** an artifact from the grid panel and **drops** it onto one of the 3 artifact sub-slots on any character. The artifact snaps into place with a satisfying pop.
4. **Swap**: If the target sub-slot already has an artifact, the old artifact returns to the grid panel and the new one takes its place (instant swap).
5. **Remove**: The player **drags** an artifact out of a sub-slot and drops it anywhere outside a valid slot (or right-clicks the sub-slot). The artifact returns to the grid.
6. **See Results**: The character's stat display on the team slot immediately updates. Base stats appear in white, and artifact bonuses appear in green parentheses: `ATK 100 (+15)`.

### What the Player Feels

- **Empowerment**: Seeing stats grow with each artifact equipped feels like tangible character investment.
- **Clarity**: The green bonus numbers make the impact of each artifact immediately obvious — no guessing.
- **Control**: Drag-and-drop feels direct and physical. The player is *placing* power onto their characters.
- **Satisfaction**: The snap animation and stat update feedback create a small dopamine hit with each equip action.

---

## 2. Interaction Flow

### 2.1 Browse & Preview Flow

```
Player opens Team Builder panel
  → Panel shows 3 team slots with artifact sub-slots
  → Player clicks "Artifacts" tab (or existing toggle)
    → Grid panel switches from character grid to Artifact Grid
      → Grid shows all owned artifacts from PlayerShopInventory
        → Artifacts already equipped on any character show a "equipped" badge
  → Player clicks any artifact card in the grid
    → ArtifactPreviewPanel slides in (right side or center overlay)
      → Shows: Icon, Name, Stat Bonuses (ATK/DEF/HP), Passive Description, Lore
      → Player clicks anywhere outside the panel
        → Panel slides out and dismisses
```

### 2.2 Equip Flow (Drag & Drop)

```
Player begins dragging an artifact card from the grid
  → A floating ghost icon follows the cursor
  → Valid drop targets (artifact sub-slots) glow/highlight
  → Player drops on an EMPTY artifact sub-slot
    → Artifact snaps into slot with pop animation
    → Grid card becomes dimmed/badged as "equipped"
    → Character stat text animates: old value → new value (green bonus appears)
    → Stat recalculation fires immediately
```

### 2.3 Swap Flow

```
Player drags an artifact from the grid to a FILLED sub-slot
  → The existing artifact ejects with a small bounce animation
  → The new artifact snaps into the slot
  → The ejected artifact returns to the grid (un-dimmed)
  → Stat text animates: old bonus replaced by new bonus

Player drags an artifact from one character's sub-slot to another character's sub-slot
  → If target slot is EMPTY: artifact moves, source slot clears
  → If target slot is FILLED: artifacts swap between the two slots
  → Both characters' stats recalculate and animate
```

### 2.4 Remove Flow

```
Player drags an artifact OUT of a sub-slot
  → Drops anywhere that is NOT a valid artifact sub-slot
    → Artifact returns to the grid panel (un-dimmed)
    → Sub-slot returns to empty state
    → Stat text animates: bonus removed, green text disappears

Player right-clicks a filled artifact sub-slot
  → Artifact is removed instantly
  → Same feedback as drag-removal
```

### 2.5 Inspect Screen Integration

```
Player opens InspectScreen (hold/click on character slot)
  → InspectScreenUI shows full character stats
  → Each stat line shows: "ATK: {base} (+{artifactBonus})" in white + green
  → Artifact section shows all 3 equipped artifacts with name + stats
  → If no artifacts equipped, section shows "No artifacts equipped"
```

---

## 3. Feedback Loops

### Equip / Swap / Remove Feedback

| Trigger | Visual | Audio | Timing |
|---------|--------|-------|--------|
| Drag starts (grid card) | Ghost icon spawns at cursor, card dims to 50% alpha | Soft "lift" click SFX | Immediate |
| Drag over valid slot | Slot border pulses glow (gold/cyan) | None | 200ms pulse loop |
| Drag over invalid area | No highlight change | None | — |
| Drop on empty slot (Equip) | Artifact icon snaps in, scale pop (1.0→1.15→1.0), slot border flash | "Equip" snap SFX (short, satisfying click-pop) | Pop: 150ms ease-out-back |
| Drop on filled slot (Swap) | Old icon slides out, new icon snaps in | "Swap" whoosh + snap SFX | Slide: 200ms, Snap: 100ms |
| Drop outside (Remove) | Ghost dissolves, slot returns to empty with fade | "Remove" soft release SFX | Dissolve: 200ms ease-out |
| Right-click remove | Icon fades out of slot, returns to grid | Same "Remove" SFX | Fade: 150ms |
| Stat text update | Numeric value tween animation (old→new), green bonus text pulse | Subtle "stat up" chime on equip, none on remove | Tween: 300ms ease-out-cubic |
| Preview panel open | Panel slides in from right edge | Soft "paper slide" SFX | 250ms ease-out-cubic |
| Preview panel dismiss | Panel slides back out to right | Soft "dismiss" SFX | 200ms ease-in-cubic |

### Grid State Feedback

| State | Visual |
|-------|--------|
| Artifact owned, unequipped | Full opacity card with icon + name |
| Artifact equipped on current team | Dimmed card (40% alpha) + small "E" badge in corner |
| Artifact being dragged | Ghost follows cursor; source card at 20% alpha |

---

## 4. Juice Specification

### Juice Spec: Artifact Build System

#### Visual Feedback
- **Screen shake**: None (this is menu UI, not combat)
- **Particle effects**: None for MVP (future: small sparkle burst on equip)
- **UI animation — Equip snap**: Scale pop 1.0→1.15→1.0, easing ease-out-back, duration 150ms
- **UI animation — Stat tween**: Number counter from old→new value, easing ease-out-cubic, duration 300ms
- **UI animation — Stat bonus color**: Green color `#4ADE80` for positive bonuses, fade-in 200ms
- **UI animation — Slot glow**: Valid drop target border pulses between normal and highlight color, 200ms cycle
- **UI animation — Preview panel**: SlideX from off-screen right to final position, ease-out-cubic, 250ms
- **Camera**: No camera changes (menu context)

#### Audio Feedback
- **Equip SFX**: Short mechanical click-pop, pitch variation ±5%, single layer
- **Swap SFX**: Quick whoosh (100ms) + snap (50ms), pitch variation ±3%
- **Remove SFX**: Soft release sound (muted click), pitch variation ±5%
- **Stat chime**: Rising tone micro-chime on stat increase, pitch matches stat delta magnitude
- **Preview open/close**: Soft paper/cloth slide, no pitch variation
- **Music**: No dynamic music response (menu BGM continues)

#### Timing
- **Input buffer**: Not applicable (menu context, not combat)
- **Drag threshold**: 10px minimum drag distance before ghost spawns (prevent accidental drags)
- **Double-click protection**: 200ms cooldown between artifact slot click actions
- **Preview dismiss delay**: 50ms delay before checking outside-click (prevent instant dismiss on open)

#### Reference
- Similar to: Genshin Impact artifact equip screen (drag/click equip, stat preview), Dead Cells gear equip (simple grid, immediate stat feedback)
- Mood: Satisfying, snappy, clean, informative

---

## 5. Edge Cases

### Spam / Rapid Input
- **Rapid drag-and-drop**: Each drop resolves fully before the next drag can begin on the same slot. Ghost cleanup is guaranteed.
- **Rapid clicking artifact slot**: 200ms cooldown between click-cycle actions prevents double-equip glitches.
- **Clicking preview then immediately dragging**: Preview panel should dismiss if a drag begins anywhere.

### Interruption / State Changes
- **Panel closed mid-drag**: If the Team Builder panel is closed while dragging, the drag cancels and the artifact returns to its original position (grid or slot).
- **Character removed from slot while artifacts equipped**: All artifacts on that character's sub-slots are automatically unequipped and returned to the grid.
- **Same artifact on multiple characters**: **Not allowed.** Each artifact instance can only be equipped on one character at a time. The grid dims equipped artifacts. Attempting to equip an already-equipped artifact should first unequip it from the other character (auto-swap behavior).

### Inventory Edge Cases
- **No artifacts owned**: Grid panel shows an empty state message: "No artifacts in inventory. Visit the Soul Shop to acquire artifacts."
- **All artifacts equipped**: Grid panel shows all cards dimmed. Player must remove an artifact to free it up.
- **Artifact sold while equipped**: If an artifact is sold from inventory while equipped (shouldn't happen in normal flow, but defensive), the artifact slot auto-clears and stats recalculate.
- **Artifact purchased**: New artifact immediately appears in the grid when the player returns to Team Builder.

### Deploy / Runtime
- **Deploy with artifacts**: When the player deploys their team to runtime (CombatZone), the artifact data must persist alongside character data. _(Current gap: DeployDraftToRuntime doesn't deploy artifacts — must be fixed.)_
- **CombatZone mode**: In CombatZone, artifact sub-slots are read-only. No drag, no click-cycle, no modification. The InspectScreen still shows artifact details.

### Scene Transitions
- **Returning to Team Builder after combat**: Draft state should restore from the last saved TeamLoadoutProfile, including artifact assignments.
- **Profile save/load**: TeamLoadoutProfile already supports artifact persistence per slot — this should be leveraged.

### Low Framerate / Performance
- **Drag performance**: Ghost follows cursor via Update, not coroutines. Should remain smooth even at 30 FPS.
- **Grid population**: Grid spawns are pooled or batched to avoid frame spikes with large artifact collections (>20 items).

---

## 6. GDD Alignment Check

### GDD Sections Fulfilled

| GDD Reference | How This Feature Aligns |
|----------------|------------------------|
| `@tag:progression` — Artifact System | "Passive and active power-ups found during Cursed Realm runs" — This feature provides the equip/management UI for those artifacts. |
| `@tag:progression` — Soul Shop Rule | "The only place where players can change, replace, or purchase their loadout (Character/Artifact)" — Artifact grid + drag-drop equip fulfills the loadout management requirement. |
| `@tag:progression` — Utility Resource table | "Artifact: Lose but Retain the Upgrade" — The system uses the existing `PlayerShopInventory` ownership model which supports this lifecycle. |
| Archived GDD §2.2 | "Artifact Equip System: Inventory-like that increases some stats or gains passive ability, character can be equipped up to 3 artifacts" — Directly fulfilled by 3 `ArtifactSlotUI` per `TeamSlotUI`. |
| Damage formula | "Combo/Buff Multiplier includes Artifact Modifiers" — Stats from equipped artifacts must flow into the combat stat pipeline (currently a gap). |

### Experience Pillar Compliance

| Pillar | Compliance |
|--------|------------|
| Fast-paced Combat | ✅ Artifact management happens in menu, not during combat. No combat pacing impact. |
| Beat Them & Acquire Them | ✅ Artifacts are acquired through Soul Shop. This feature handles the *equip* side. |
| Rogue-lite Mechanics | ✅ Artifact equip/unequip is part of the between-run loadout preparation loop. |

### Experience Pillar Violations
- **None.** This is a menu/inventory feature that supports — but does not interfere with — combat pacing.

### Primary Platform Input Model
- **PC Keyboard & Mouse** (`@tag:platform-input`): All interactions designed for mouse (click, drag, right-click). No gamepad-specific flows needed for MVP.

---

## 7. Playtest Checkpoint

### When to Playtest
After implementation is complete and all acceptance criteria pass, conduct a focused playtest session.

### What to Look For
1. **Equip flow clarity**: Can the player figure out how to equip artifacts without a tutorial? Watch for confusion.
2. **Stat feedback readability**: Are the green bonus numbers visible enough? Do players notice their stats changing?
3. **Drag precision**: Can players reliably drop artifacts on the small sub-slots? If miss rate is >20%, consider larger drop targets.
4. **Swap intuition**: When dropping on a filled slot, does the player expect the swap behavior? Or do they expect rejection?
5. **Preview panel usefulness**: Do players use the preview panel? Is the information sufficient for decision-making?
6. **Dismiss behavior**: Does clicking outside the preview panel feel natural? Any frustration with accidental dismissals?
7. **Overall pace**: Does the artifact management feel quick enough for rogue-lite between-run pacing? If equipping 3 artifacts takes >15 seconds, the flow is too slow.

### Listen For
- "Where did my artifact go?" → Equip/swap feedback unclear
- "What does this artifact do?" → Preview panel information insufficient
- "I can't drag it there" → Drop target too small or feedback unclear
- "My stats didn't change" → Stat recalculation or display broken

---

## 8. Scope Notes for Architect Agent

### In Scope (This Task Card)
- `ArtifactGridPanel` showing all owned artifacts
- Drag-and-drop equip/swap/remove between grid and character artifact sub-slots
- Stat recalculation and display with green bonus text
- `ArtifactPreviewPanel` with click-to-open and click-outside-to-dismiss
- InspectScreenUI stat display updates
- Artifact deployment to runtime (fix the `DeployDraftToRuntime` gap)

### Out of Scope (Noted in Task Card)
- Soul Shop purchase algorithms
- Artifact sprite/icon creation
- Artifact rarity/tier system (only flat stats for now)
- Artifact set bonuses
- Artifact leveling/enhancement
- Artifact passive effect runtime execution (data model supports `description` field but no gameplay effect system)
- Gamepad controller support for artifact management

### Existing Code to Leverage
- `ArtifactSlotUI.cs` — refactor from click-cycle to drop target
- `CharacterGridInventoryUI.cs` — pattern for artifact grid (reuse spawning/dimming logic)
- `CharacterGridItemUI.cs` — pattern for draggable artifact cards
- `TeamSlotUI.RecalculateStats()` — extend for animated stat feedback
- `InspectScreenUI.BuildArtifactSummary()` — expand with better formatting
- `TeamLoadoutProfile.cs` — already supports artifact persistence per slot
- `TeamBuilderUI.GetOwnedArtifacts()` — filtering owned artifacts from inventory
