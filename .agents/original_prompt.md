## 2026-06-06T21:32:16+07:00

Refactor Aura Energy rates/chances, fix projectile infinite hits, remove Aura Energy rate bonus in Artifact, sync equipped Artifacts with TeamUI, and clean up audio sections in CharacterObject in the Unity project.

Working directory: c:\Users\Apricha\Documents\GitHub\Thesis_
Integrity mode: development

## Requirements

### R1. Aura Energy Gain of Chance Refactor
Refactor the Aura Energy gain chance system:
- Return the AuraEnergyRate categories: High (15%), Med (10%), Low (5%).
- Ensure the chance is checked on every single hit rather than only on enemy kill.
- If no HitboxData is being sent out (or baseGainAmount <= 0f), there should be no AE chance routine.

### R2. Projectile Hit Count and HitData Fix
Fix projectile hit count logic:
- Projectiles currently hit infinitely until expiration when a character has no Artifact equipped; restore respect for Hit Count by registering hits unconditionally.
- Fix HitData in Debug mode which is currently not showing the hit details or data even though in-game enemies receive damage.

### R3. Remove Aura Energy Rate from Artifact Field
Remove the Aura Energy Rate of CharacterStats bonus in the Artifact field so it is not visible or editable in the Artifact inspector.

### R4. Sync Artifact Data with TeamUI
Ensure that Artifacts equipped on `CharacterRuntime` are correctly synced with the TeamUI, so that reopening the TeamUI does not lose or reset the character's Artifact configuration.

### R5. Clean Up CharacterObject and Audio
Restore the `audioData` field on `CharacterObject` and clean up `CharacterMetaData` by removing old unused fields `attackSfx`, `skillSfx`, and `hurtSfx`.

## Acceptance Criteria

### AE Chance and hit box
- [ ] AE chance routine rolls on every hit based on AuraEnergyRate: High (15%), Med (10%), Low (5%).
- [ ] AE chance routine does not run when no hitbox data is active (e.g. no energy amounts configured).

### Projectile and Debug Data
- [ ] Projectiles respect hit counts and do not hit infinitely when no Artifact is equipped.
- [ ] HitData is populated and displayed correctly in Debug mode during combat.

### Artifact Field and UI Sync
- [ ] Aura Energy Rate is removed from Artifact stats.
- [ ] Equipped Artifacts persist and sync correctly when reopening the TeamUI.

### CharacterObject Cleanup
- [ ] Audio section is restored on CharacterObject (`audioData` field) and references are updated to use it.
- [ ] Old SFX fields are removed from CharacterMetaData.
