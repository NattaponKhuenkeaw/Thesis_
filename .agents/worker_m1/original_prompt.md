## 2026-06-06T14:42:42Z
Objective: Implement the combat and stats changes described in ORIGINAL_REQUEST.md for the follow-up request (R1 to R5).

Refer to the analysis reports from Explorer 1, 2, and 3:
- c:\Users\Apricha\Documents\GitHub\Thesis_\.agents\explorer_m1_1\analysis.md
- c:\Users\Apricha\Documents\GitHub\Thesis_\.agents\explorer_m1_2\analysis.md
- c:\Users\Apricha\Documents\GitHub\Thesis_\.agents\explorer_m1_3\analysis.md

Implement the following tasks:
1. R1 (Aura Energy Gain Chance):
   - Edit Assets/Project/Scripts/Character/CharacterStats.cs to restore the AuraEnergyRate enum (Low, Med, High) and the auraEnergyRate field on CharacterStats (default to AuraEnergyRate.Med). Copy auraEnergyRate in Clone(). Keep AddNumericBonuses unmodified.
   - Edit Assets/Project/Scripts/Combat/PlayerCombat.cs's NotifyResolvedEnemyHit() to check the AE chance roll on every hit (kills and non-kills) by moving it outside the if(!isDefeated) check. Use ResolveAuraEnergyGainChance(auraEnergyRate) or matching logic (Low=5%, Med=10%, High=15%). Early exit if baseGainAmount <= 0f so the AE chance routine does not run when no HitboxData is active.
2. R2 (Projectile Hit Count & Debug):
   - Edit Assets/Project/Scripts/Combat/HitBoxDamage.cs in ProcessHit() to call projectileOwner?.RegisterSuccessfulHit(enemy) unconditionally (remove it from inside the if(dealtDamage) block).
   - Edit Assets/Project/Scripts/Combat/TrainingDebugManager.cs and Assets/Project/Scripts/Combat/TrainingDebugStats.cs's Awake() to update singleton duplicate-destruction logic: check if the existing instance is in the same scene before destroying the gameObject:
     if (_instance != null && _instance != this)
     {
         if (_instance.gameObject.scene == gameObject.scene)
         {
             Destroy(gameObject);
             return;
         }
     }
     _instance = this;
3. R3 (Remove AE Rate from Artifact Field):
   - Create a Custom Editor for Artifact: Assets/Project/Scripts/Editor/ArtifactEditor.cs. In it, manually draw the bonusStats child properties while skipping auraEnergyRate to prevent it from being visible or editable in the Artifact inspector.
4. R4 (Sync Artifacts with TeamUI):
   - Review Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs's LoadRuntimeDraft() to ensure equipped artifacts are synced correctly and not lost when reopening TeamUI. (Verify no compile or logic errors exist).
5. R5 (Clean Up CharacterObject & Audio):
   - Edit Assets/Project/Scripts/Character/CharacterObject.cs to restore "public PlayerAudioData audioData;" field (with [Header("Audio")]).
   - Edit Assets/Project/Scripts/Systems/PlayerCharacter.cs in InitializeAudio() to pass characterObject.audioData to Audio.Initialize(this, characterObject.audioData) instead of hardcoded null.
   - Edit Assets/Project/Scripts/Character/CharacterMetaData.cs to remove obsolete SFX fields (attackSfx, skillSfx, hurtSfx).
