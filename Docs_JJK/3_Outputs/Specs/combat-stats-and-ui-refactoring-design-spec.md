# Design Spec: Character Stats, Combat Formula Refactor, and TeamBuilder UI Split

## 1. Player Experience Description

From the player's perspective, character development and team preparation are divided into clear strategic decisions and immediate combat pay-offs:

- **Progression and Loadout Phase (Safe Zones - Lobby / Soul Shop)**:
  When visiting safe zones like the Lobby or the Soul Shop, the player opens the Team Builder panel. The game transitions to a full-screen menu overlay that completely freezes the background gameplay. The player sees their team's portraits in three slots alongside a grid of all owned characters.
  The player can drag and drop portraits to reorder the party, swap characters out, and manage equipped artifacts. Adjusting stats feels responsive: equipping an artifact instantly recalculates the character's basic stats (ATK, DEF, HP) and special stats, updating the display with a green text highlight, a numeric count-up animation, and mechanical click sounds.
  
- **Action Phase (Combat Zones - Rooms / Boss Arenas)**:
  Once in a run, the player cannot modify their loadout. Opening the Team UI displays a read-only "Normal Team Data" sheet which also pauses the game completely. The inventory grid is hidden, and slots show a lock icon. Dragging portraits does nothing but trigger a muffled error tone and a horizontal wiggle of the slot, reminding the player that loadouts are locked.
  
- **Inspect Screen Sub-Panel**:
  Clicking any character card in the Combat grid opens a detailed **Inspect Screen**. This sub-panel slides in from the right, dividing the screen into three distinct visual regions:
  - **Left Section (Identity & Lore)**: A high-resolution character portrait, element icon, and scrollable lore backstory.
  - **Middle Section (Stats & Passives)**: Displays all 9 combat stats in the `"Base(+Additional)"` format (e.g. `10(+2)` for SPD, `100(+25)` for HP, `10(+5)` for ATK), followed by a list of active passive skills.
  - **Right Section (Artifacts & Potential)**: Shows detailed stats and passives for the 3 equipped artifacts, alongside a vertical track of the character's 6 Potential levels (current level highlighted in gold, locked levels dimmed). Clicking a Potential level shows its permanent upgrade benefits.
  
- **Combat Feel and Feedback**:
  During active combat, the player immediately feels the impact of their stat builds:
  - **ATK, DMG Mastery, and SKILL Mastery**: Upgrading these stats makes damage numbers float up larger. SKILL Mastery selectively makes Skills and Ultimates hit with massive, heavy floaters.
  - **CRIT and CRIT DMG**: Landing a critical hit triggers a satisfying frame freeze (hit-stop), a heavy screen shake, and a large, high-contrast yellow/orange damage number pop-up, accompanied by a crunchy tearing sound.
  - **Speed (SPD)**: A higher SPD stat makes horizontal running feel light and rapid, giving the player a clear mechanical advantage for dodging telegraphs.
  - **Cooldown Reduction (CDR)**: Active skills and ultimates cool down faster. The circular cooldown sweep overlay moves rapidly, and a clean chime rings when the skill is ready for action.
  - **DEF and DMG Res**: When hit, players with high defense see small red damage numbers, and they easily absorb attacks that would otherwise stagger or deplete their HP.

---

## 2. Interaction Flow Diagram

### 1. Combat Stats Calculation Flow
```
Player strikes an enemy with a Combat Action (e.g., Skill/Ultimate)
  → Base Damage = Attacker Total ATK * ActionMultiplier
  → Critical Roll: Random(0, 100) <= Attacker CRIT Rate
    ├─ If True: DamageAfterCrit = Base Damage * (Attacker CRIT DMG / 100)
    │           → Trigger: Heavy Hit-stop (6 frames) + Camera Shake + Yellow/Red Bold Damage Popup
    └─ If False: DamageAfterCrit = Base Damage
                → Trigger: Light Hit-stop (2 frames) + Standard White Damage Popup
  → Apply Masteries: Outgoing Damage = DamageAfterCrit * (1 + Attacker DMG Mastery / 100)
    ├─ If Action is Skill or Ultimate:
    │  Outgoing Damage = Outgoing Damage * (1 + Attacker SKILL Mastery / 100)
  → Defender Defense Mitigation: DamageAfterDEF = Outgoing Damage * [100 / (100 + Defender DEF)]
  → Defender Resistance Mitigation: MitigatedDamage = DamageAfterDEF * (1 - Defender DMG Res / 100)
  → Final Damage Received = Max(1.0, MitigatedDamage)
  → Target HP is reduced
```

### 2. UI Menu Open and Pause Flow
```
Player presses Team UI Key (e.g. "Tab")
  → System checks active zone type (Safe Zone vs. Combat Zone)
  → Freeze Gameplay: Set Time.timeScale = 0.0
  → Muffle Audio: Mute / apply Low-Pass Filter on active gameplay sound effects (-10dB)
  → Shift Input: Swap input action map to "UI navigation" and disable character controls
  → Render Full-Screen Background Blur
    ├─ If In Safe Zone (Lobby/Soul Shop):
    │  └─ Load "Interactive Team Builder Mode"
    │         → Display character inventory grid
    │         → Enable slot drag-and-drop swaps & artifact modifications
    │         → Enable hover visual scaling & placement clicks
    └─ If In Combat Zone (Rooms/Boss Arenas):
       └─ Load "Normal Team Data Mode"
             → Hide character inventory grid
             → Show padlock overlays on team slots
             → Disable drag-and-drop swaps & modifications
             ├─ Player tries to drag slot → Trigger: Lock visual wiggle + Padlock pulse + Error SFX
             └─ Player clicks character slot:
                └─ Slide in "Inspect Screen Sub-Panel"
                      → Populate Lore & Sprite (Left)
                      → Populate All 9 Stats (Middle) in "Base(+Additional)" format
                      → Populate Equipped Artifacts & Potential Levels 1-6 (Right)
```

### 3. Inspect Screen Sub-Panel Layout Flow
```
Inspect Sub-Panel Opened
  ├─ Left Column:
  │    → Load Display Name & Element Icon
  │    → Load Portrait sprite (from CharacterMetaData)
  │    → Read and render lore text (from CharacterMetaData.description)
  ├─ Middle Column:
  │    → Render 9 stats as "Base(+Additional)" text rows
  │    → Load unlocked Passive Nodes list from CharacterProgressionDefinition
  └─ Right Column:
       → Display 3 Artifact slot items with detail pop-ups on hover
       → Render 6 Potential levels in vertical path
             → Fetch character progression state rank
             → Highlight active levels (gold) / dim locked levels (gray)
             → User hovers/clicks Potential level:
                  └─ Display popup tooltip with node details & unlock status
```

---

## 3. Feedback Loops Table

| Trigger | Visual | Audio | Timing |
|---------|--------|-------|--------|
| **Critical Hit Landed** | Large bold font popup (yellow-to-orange gradient), high-tier camera shake, double-length hit-stop. | Crunchy slash/impact sound layered with a glass-breaking ring. | 0.4s fade-out, hit-stop: 6 frames. |
| **Stat Boost Equipping** | Stat UI text flashes green, scales up to 1.2x, and rolls numbers upward. | Light digital sweep sound (soft pitch rise). | 0.3s duration, ease-out-quad scaling. |
| **Skill Ready (CDR Finish)** | Circular icon cooldown sweep finishes; icon flashes white with a diagonal glint. | High-pitch chime or metallic ring. | 0.15s flash, linear fade. |
| **Locomotion at High SPD** | Wind streak particles spawn behind the running character. | High-pitched clothing rustle or air whoosh. | Continuous during runs if SPD is >130% base. |
| **Locked Drag Attempt** | Team slot card wiggles horizontally; red padlock icon pulses. | Dull, low-pitched mechanical click (error chime). | 0.25s wiggle, sine-wave decay. |
| **Successful Drag Swap** | Portals glow; portraits snap into place with a small radial particle blast. | Heavy mechanical latch sound. | 0.12s snap, ease-out-cubic. |
| **Full-Screen UI Opened** | Smooth fade-in overlay, gameplay background blurs out focus. | Dynamic high-cut low-pass filter sweeps gameplay sound down. | 0.4s fade-in (unscaled time). |
| **Potential Level Hover** | Node scales up to 1.1x, border glows, tool-tip panel fades in. | Soft synth hum. | 0.1s transition, linear. |

---

## 4. Juice Specification

### Juice Spec: Combat Stats and UI Refactoring

#### Visual Feedback
- **Critical Hit Damage Popups**:
  - Font: Heavy, bold, 30% larger than normal hit text.
  - Color: Gradient from bright yellow (`#FFE500`) to deep orange/red (`#FF3C00`) with a dark gray outline (`#1C1C1C`).
  - Motion: Pops vertically with a random +/- 15 degree angle, bounces, and floats upward while fading.
- **Skill/Ultimate Ready Flash**:
  - Scale Punch: Scale up to 1.25x for 0.08s, then settle.
  - Glint: Diagonal white flare sweep at 45 degrees.
- **Normal Team Data Lock Visuals**:
  - Padlock icon displayed on the top-right corner of each team slot when locked.
  - Interactive grid panel is slide-collapsed out of view.
  - Locked slots are shaded with a 40% desaturated overlay.
- **UI Error Wiggle**:
  - Horizontal translation: +/- 8 pixels.
  - Easing: Sine-wave decay.
  - Duration: 15 frames at 60fps (0.25s).
- **Stat Value Display Format**:
  - Speed (SPD) format: `"Base(+Additional)"` (e.g., `10(+2)`).
  - Basic stats (ATK, DEF, HP) format: `"Base(+Additional)"` (e.g., `10(+5)` ATK).
- **Background Pausing Overlay Visuals**:
  - Gaussian blur filter applied dynamically over active gameplay camera.
  - Canvas alpha fade-in from 0 to 1 over 0.4s.

#### Audio Feedback
- **Normal Hit vs. Crit Hit**:
  - Normal Hit: Mid-range impact sound (flat pitch).
  - Crit Hit: Layered heavy impact (lower pitch) + higher frequency "crunch". Pitch variation +/- 10%.
- **UI Interactivity**:
  - Safe Zone Placement: Solid wooden/metallic click-latch.
  - Locked Zone Action: Low-pitch, damp thud.
- **Ducking Settings during Pause**:
  - Low-pass filter frequency: 350 Hz.
  - Gameplay volume reduction: -10dB.
  - UI sounds remain at full volume.

#### Physical Feedback
- **Controller Haptics**:
  - Critical Hit: Left heavy motor at 0.75, right light motor at 0.5, duration 0.15s.
  - Standard Hit: Both motors at 0.25, duration 0.05s.

#### Timing
- **Input Buffer**: 12 frames.
- **Coyote Time**: 12 frames.
- **Hit-stop (Hit Freeze)**:
  - Normal Hit: 2 frames.
  - Critical Hit: 6 frames.
  - Ultimate Finisher Hit: 12 frames.
- **UI Snap Animation**: 0.12 seconds linear interpolation.
- **Menu Pause Transition**: 0.4s fade-in/fade-out using `Time.unscaledDeltaTime`.

#### Reference
- **Similar to**: *Dungeon Slasher* and *Oblivion Override* for snappy UI response and locked mid-run progression interfaces.
- **Mood**: Snappy, systematic, robust, and clean.

---

## 5. Edge Cases

- **What happens if the player spams drag inputs in the Team Builder?**
  - While a swap transition is animating (0.12s), raycasting on all team slots and character cards must be temporarily disabled via CanvasGroup interaction locks, preventing visual overlap or slot duplication.
  
- **What happens if the player opens the Team UI at the exact frame of transition between a safe zone and a combat zone?**
  - The UI state check occurs during the `OnPanelOpened` lifecycle. If the state is transition-unsafe, the system must default to the most restrictive mode (Normal Team Data - Read-only) to prevent characters or artifacts from being edited inside combat rooms.
  
- **What happens if the player is interrupted/takes damage while opening the UI in a combat zone?**
  - Since opening the UI in a combat zone freezes time scale (`Time.timeScale = 0`), gameplay is paused and enemies cannot move or attack. Therefore, the player cannot take damage while reading the UI.
  - Input buffers are cleared upon opening the UI, preventing cached attacks or dashes from executing immediately when the UI closes.
  
- **What happens to active drags on scene transitions?**
  - If a scene transition occurs (e.g. entering a portal to start a run) while the player is dragging a portrait in the Team Builder, the drag session is cancelled, and the dragged portrait snaps back to its original slot.
  
- **What happens on low-framerate devices?**
  - All stat scaling for movement (SPD) must be calculated in `FixedUpdate` using `Time.fixedDeltaTime` to ensure locomotion speeds do not behave erratically.
  - Cooldown timers (CDR) must tick based on `Time.unscaledDeltaTime` or scaled `Time.deltaTime` depending on pause state, ensuring that skill recovery times are identical across varying framerates.

---

## 6. GDD Alignment Check

### GDD Section Alignment
- Fulfills **`@tag:characters`**: Expands the character stats structure to support ATK, DEF, SPD, CDR, CRIT, CRIT DMG, SKILL Mastery, DMG Mastery, and DMG Res.
- Fulfills **`@tag:combat`**: Implements standardized formulas for damage output (attacker-side scaling, crits, masteries) and damage mitigation (defender-side defense curves and resistance).
- Fulfills **`@tag:progression`**: Enforces safe zone interaction policy, dividing the UI between safe-haven modifications (Lobby/Soul Shop) and locked-down active runs.

### Experience Pillars
- **Fast-paced Combat Hack & Slash**: Visual/audio pacing (large crit popups, CDR sweeps, hit-stop tiers) aligns directly with the pillar of high-impact visuals.
- **Rogue-lite Mechanics**: Upgrades from the Soul Shop or during a run map directly to these 9 defined stats, providing transparent progression goals.

### Platform Support
- Designed first for **PC Keyboard/Mouse**.
  - Left Mouse Button (LMB) handles dragging and clicking elements.
  - Mouse hovers control scale-punch previews.
  - Keyboard numbers (1, 2, 3) support hotkeys for quick-selecting active slots.
  - Keyboard shortcut (e.g., Tab or Esc) toggles opening and closing the panel.
- Gamepad-ready: Left stick maps to slot grid navigation, and button mappings replace drag-and-drop operations with select-to-swap behavior.

---

## 7. Playtest Checkpoint Recommendation

To verify the game feel and structural integrity of this spec:

1. **Phase 1: Safe Zone UI Verification**
   - Open the Team Builder in the Lobby. Confirm that the background gameplay pauses and background music becomes muffled.
   - Drag and drop characters. Verify that dragging works smoothly, cards snap into slots in exactly `0.12 seconds`, and stats numbers update dynamically with green flashes and the equip latch sound.
   
2. **Phase 2: Combat Zone Lock Verification**
   - Enter a Combat Room. Open the Team UI. Confirm gameplay freezes.
   - Confirm the grid inventory is hidden.
   - Attempt to drag slots. Verify that slots wiggle, display a padlock icon, and play the error click sound.
   - Click a character. Verify that the **Inspect Screen Sub-Panel** slides in. Confirm that all 9 stats are formatted as `"Base(+Additional)"` (e.g., `10(+2)`), and check that Potential levels 1 to 6 show their lock/unlock states properly.
   
3. **Phase 3: Stat and Combat Formula Calibration**
   - Use debug upgrades to test stat changes:
     - Set **CRIT** to 100% and test basic attacks. Verify that every hit trigger displays the yellow/orange bold popup, 6-frame hit-stop, and heavy camera shake.
     - Set **CDR** to 50%. Verify that a 10s cooldown skill takes exactly 5s to refresh, and that the circular UI sweep tracks this correctly.
     - Set **SPD** to +50% (SPD total = 15, base = 10). Verify that horizontal locomotion feels noticeably faster and matches the movement speed calculation.
     - Set **DEF** to 100 and **DMG Res** to 50%. Verify that taking hits from standard enemies results in significantly reduced damage numbers.
