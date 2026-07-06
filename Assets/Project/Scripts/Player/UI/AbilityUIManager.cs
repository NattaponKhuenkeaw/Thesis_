using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives both the Skill and Ultimate ability UI slots.
///
/// Per-slot elements (Skill and Ultimate each have their own set):
///   CooldownOverlay  — fillAmount 1→0 while cooling down;
///                      on expiry: rushes 0→1 then deactivates ("ready rush").
///   Icon             — alpha 1.0 when ready, 0.39 (≈100/255) when not ready.
///   FollowUpOverlay  — fillAmount 1→0 during an active follow-up window.
///                      Shown whenever a window is active, no ground-condition gate.
///   CountdownText    — counts down remaining cooldown seconds.
///
/// Ultimate only:
///   AEOverlay        — fillAmount 0→1 as AE charges toward cost.
///                      Always visible; draws independently of cooldown state.
/// </summary>
public class AbilityUIManager : MonoBehaviour
{
    public static AbilityUIManager Instance;

    public enum AbilityType { Skill, Ultimate }

    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;

    // ─── Skill Slot ──────────────────────────────────────────────────────────
    [Header("Skill Slot")]
    [SerializeField] private Image skillCooldownOverlay;
    [SerializeField] private Image skillIcon;
    [SerializeField] private Image skillFollowUpOverlay;
    [SerializeField] private TMP_Text skillCountdownText;
    [SerializeField] private RectTransform skillFeedbackRoot;

    // ─── Ultimate Slot ───────────────────────────────────────────────────────
    [Header("Ultimate Slot")]
    [SerializeField] private Image ultCooldownOverlay;
    [SerializeField] private Image ultIcon;
    [SerializeField] private Image ultFollowUpOverlay;
    [SerializeField] private TMP_Text ultCountdownText;
    [SerializeField] private Image ultAEOverlay;
    [SerializeField] private RectTransform ultFeedbackRoot;

    // ─── Ready Rush ──────────────────────────────────────────────────────────
    [Header("Ready Rush")]
    [SerializeField] private float readyRushDuration = 0.15f;

    // ─── Ready Feedback ──────────────────────────────────────────────────────
    [Header("Ready Feedback")]
    [SerializeField] private bool playFallbackReadyFeedback = true;
    [SerializeField] private Color readyGlintColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color readyBurstColor = new Color(0.6f, 1f, 0.85f, 0.9f);
    [SerializeField] private float readyPulseScale = 1.25f;
    [SerializeField] private float readyFeedbackDuration = 0.18f;

    // ─── Rejected Feedback ───────────────────────────────────────────────────
    [Header("Rejected Feedback")]
    [SerializeField] private Color rejectedFlashColor = new Color(1f, 0.2f, 0.15f, 0.9f);
    [SerializeField] private float rejectedWiggleDistance = 8f;
    [SerializeField] private float rejectedFeedbackDuration = 0.18f;

    // ─── Aura Gain Feedback ──────────────────────────────────────────────────
    [Header("Aura Gain Feedback")]
    [SerializeField] private Color auraGainGlintColor = new Color(0.45f, 1f, 0.75f, 0.55f);
    [SerializeField] private float auraGainPulseScale = 1.04f;
    [SerializeField] private float auraGainFeedbackDuration = 0.1f;
    [SerializeField] private float auraGainFeedbackMinInterval = 0.08f;

    // ─── Alpha constants ─────────────────────────────────────────────────────
    private const float AlphaReady    = 1.00f;          // ability usable
    private const float AlphaDimmed   = 100f / 255f;    // ≈ 0.392 — on cooldown / not ready

    // ─────────────────────────────────────────────────────────────────────────
    // Internal Data
    // ─────────────────────────────────────────────────────────────────────────

    private struct CooldownDisplayState
    {
        public float current;
        public float max;
    }

    private struct FollowUpWindowState
    {
        public bool  active;
        public float remaining;
        public float duration;
    }

    /// <summary>Per-slot ready-rush state.</summary>
    private struct SlotRushState
    {
        public bool  isRushing;
        public float rushTimer;   // counts UP from 0 → readyRushDuration
    }

    private readonly Dictionary<string, CooldownDisplayState> skillCooldowns     = new();
    private readonly Dictionary<string, CooldownDisplayState> ultimateCooldowns  = new();

    private SlotRushState skillRush;
    private SlotRushState ultRush;

    private PlayerCharacter subscribedPlayer;
    private string          displayedCharacterKey      = string.Empty;
    private UIFeedbackDriver feedbackDriver;
    private TeamLoader       teamLoader;

    private float lastObservedUltimateAE     = -1f;
    private float lastAuraGainFeedbackTime   = -999f;

    // "was-ready" trackers — used to fire the ready pulse exactly once per transition
    private bool skillWasReadyLastFrame;
    private bool ultimateWasReadyLastFrame;
    private bool skillFollowUpWasActiveLastFrame;
    private bool ultimateFollowUpWasActiveLastFrame;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        CacheReferences();
        feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);
        ResetSlot(skillCooldownOverlay, skillCountdownText, skillFollowUpOverlay, skillIcon);
        ResetSlot(ultCooldownOverlay, ultCountdownText, ultFollowUpOverlay, ultIcon);
        SetOverlayFill(ultAEOverlay, 0f, active: false);
    }

    private void Start()
    {
        RebindPlayer();
        RefreshActiveCharacterUI();
    }

    private void OnEnable()
    {
        RebindPlayer();
        if (teamLoader != null)
        {
            teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;
            teamLoader.OnCharacterAuraEnergyUpdated += HandleCharacterAuraEnergyUpdated;
        }
        RefreshActiveCharacterUI();
    }

    private void OnDisable()
    {
        UnbindPlayer();
        if (teamLoader != null)
            teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;
    }

    private void Update()
    {
        CacheReferences();
        RebindPlayer();

        // Tick rush animations first (before refresh, so we read correct rush state)
        TickRush(ref skillRush,   AbilityType.Skill,   skillCooldownOverlay,   skillIcon);
        TickRush(ref ultRush,     AbilityType.Ultimate, ultCooldownOverlay,    ultIcon);

        // Tick cooldown timers; on expiry → start rush instead of immediate clear
        TickCooldownDictionary(skillCooldowns,    AbilityType.Skill,    ref skillRush);
        TickCooldownDictionary(ultimateCooldowns, AbilityType.Ultimate, ref ultRush);

        string currentCharacterKey = GetCurrentCharacterKey();
        if (currentCharacterKey != displayedCharacterKey)
        {
            displayedCharacterKey              = currentCharacterKey;
            ultimateWasReadyLastFrame          = false;
            skillWasReadyLastFrame             = false;
            skillFollowUpWasActiveLastFrame    = false;
            ultimateFollowUpWasActiveLastFrame = false;
            lastObservedUltimateAE             = GetCurrentAuraEnergy();
        }

        RefreshActiveCharacterUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void TriggerCooldown(AbilityType type, float duration)
    {
        if (duration <= 0f)
        {
            RefreshActiveCharacterUI();
            return;
        }

        string characterKey = GetCurrentCharacterKey();
        if (string.IsNullOrEmpty(characterKey)) return;

        Dictionary<string, CooldownDisplayState> targetDictionary =
            type == AbilityType.Skill ? skillCooldowns : ultimateCooldowns;

        targetDictionary[characterKey] = new CooldownDisplayState
        {
            current = duration,
            max     = duration
        };

        // Cancel any in-progress ready-rush for this slot when a new cooldown starts
        if (type == AbilityType.Skill)
            skillRush = default;
        else
            ultRush = default;

        RefreshActiveCharacterUI();
    }

    public void PlayAbilityRejectedFeedback(AbilityType type)
    {
        RectTransform targetRect = ResolveFeedbackRoot(type);
        if (targetRect == null) return;

        EnsureFeedbackDriver();
        if (feedbackDriver == null) return;

        Vector2 origin = targetRect.anchoredPosition;
        feedbackDriver.PlayWiggle(targetRect, origin, rejectedWiggleDistance, rejectedFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, rejectedFlashColor, rejectedFeedbackDuration);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cooldown Timer Tick
    // ─────────────────────────────────────────────────────────────────────────

    private void TickCooldownDictionary(
        Dictionary<string, CooldownDisplayState> cooldowns,
        AbilityType type,
        ref SlotRushState rush)
    {
        if (cooldowns.Count == 0) return;

        List<string> keysToClear = null;
        List<string> keys        = new List<string>(cooldowns.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            string key   = keys[i];
            CooldownDisplayState state = cooldowns[key];
            state.current = Mathf.Max(0f, state.current - Time.deltaTime);

            if (state.current <= 0f || state.max <= 0f)
            {
                keysToClear ??= new List<string>();
                keysToClear.Add(key);

                // Kick off ready-rush for the current character's slot
                string currentKey = GetCurrentCharacterKey();
                if (string.Equals(key, currentKey, System.StringComparison.Ordinal)
                    && !rush.isRushing)
                {
                    rush = new SlotRushState { isRushing = true, rushTimer = 0f };
                }
                continue;
            }

            cooldowns[key] = state;
        }

        if (keysToClear == null) return;
        for (int i = 0; i < keysToClear.Count; i++)
            cooldowns.Remove(keysToClear[i]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ready-Rush Tick
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the rush animation (fillAmount 0→1) and fires the ready pulse
    /// when the rush completes. Must be called every frame before RefreshActiveCharacterUI.
    /// </summary>
    private void TickRush(
        ref SlotRushState rush,
        AbilityType type,
        Image cooldownOverlay,
        Image icon)
    {
        if (!rush.isRushing) return;

        rush.rushTimer += Time.deltaTime;
        float t = readyRushDuration > 0f
            ? Mathf.Clamp01(rush.rushTimer / readyRushDuration)
            : 1f;

        // Rush fill: 0 → 1
        SetOverlayFill(cooldownOverlay, t, active: true);
        // Icon is immediately ready during the rush (ability is usable)
        SetIconAlpha(icon, AlphaReady);

        if (t >= 1f)
        {
            // Rush complete: deactivate overlay and fire feedback
            rush = default;
            SetOverlayFill(cooldownOverlay, 0f, active: false);
            PlayAbilityReadyFeedback(type);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main Refresh
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshActiveCharacterUI()
    {
        string characterKey = GetCurrentCharacterKey();
        displayedCharacterKey = characterKey;

        UpdateSkillUI(characterKey);
        UpdateUltimateUI(characterKey);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Skill UI
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateSkillUI(string characterKey)
    {
        // While rush is playing, TickRush() owns the overlay — skip cooldown draw
        if (skillRush.isRushing)
        {
            // Still update follow-up and countdown even during rush
            UpdateFollowUpOverlay(AttackCategory.Skill, skillFollowUpOverlay,
                ref skillFollowUpWasActiveLastFrame, AbilityType.Skill);
            SetTextActive(skillCountdownText, false);
            skillWasReadyLastFrame = true;
            return;
        }

        // ── Cooldown ────────────────────────────────────────────────────────
        bool onCooldown = skillCooldowns.TryGetValue(characterKey, out CooldownDisplayState cd)
                          && cd.max > 0f && cd.current > 0f;

        if (onCooldown)
        {
            // fillAmount 1 → 0  (full overlay, drains as time passes)
            float ratio = Mathf.Clamp01(cd.current / cd.max);
            SetOverlayFill(skillCooldownOverlay, ratio, active: true);
            SetIconAlpha(skillIcon, AlphaDimmed);
            SetCountdownText(skillCountdownText, cd.current);
        }
        else
        {
            SetOverlayFill(skillCooldownOverlay, 0f, active: false);
            SetIconAlpha(skillIcon, AlphaReady);
            SetTextActive(skillCountdownText, false);

            // Fire ready pulse on the transition to ready
            if (!skillWasReadyLastFrame)
                PlayAbilityReadyFeedback(AbilityType.Skill);
        }

        skillWasReadyLastFrame = !onCooldown;

        // ── Follow-Up Overlay ────────────────────────────────────────────────
        UpdateFollowUpOverlay(AttackCategory.Skill, skillFollowUpOverlay,
            ref skillFollowUpWasActiveLastFrame, AbilityType.Skill);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ultimate UI
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateUltimateUI(string characterKey)
    {
        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        if (currentCharacter == null || teamLoader == null)
        {
            ResetSlot(ultCooldownOverlay, ultCountdownText, ultFollowUpOverlay, ultIcon);
            SetOverlayFill(ultAEOverlay, 0f, active: false);
            ultimateWasReadyLastFrame          = false;
            ultimateFollowUpWasActiveLastFrame = false;
            lastObservedUltimateAE             = -1f;
            return;
        }

        // ── Resolve ultimate action data ─────────────────────────────────────
        // Use context-free resolution so cooldown display works regardless of
        // whether the player is grounded or airborne.
        UltimateAction resolvedAction = ResolveDisplayedActionContextFree(AttackCategory.Ultimate) as UltimateAction;

        float currentAE        = teamLoader.GetCharacterCurrentAE(currentCharacter);
        float maxAE            = teamLoader.GetCharacterMaxAE(currentCharacter);
        float requiredAE       = resolvedAction != null
            ? teamLoader.GetUltimateCost(currentCharacter, resolvedAction)
            : teamLoader.GetUltimateCost(currentCharacter);
        float resolvedCooldown = resolvedAction != null ? resolvedAction.cooldown : 0f;
        float remainingCooldown = resolvedAction != null
            && playerCharacter != null
            && playerCharacter.Combat != null
            ? playerCharacter.Combat.GetRemainingCooldown(
                AttackCategory.Ultimate,
                resolvedAction.cooldown,
                resolvedAction.actionName,
                resolvedAction.actionId)
            : 0f;

        bool cooldownBlocked = remainingCooldown > 0.01f;
        bool energyReady     = requiredAE <= 0.01f || currentAE + 0.01f >= requiredAE;
        bool isReady         = !cooldownBlocked && energyReady;

        // ── AE Overlay — always visible, 0 → 1 ──────────────────────────────
        float aeRatio = requiredAE > 0.01f
            ? Mathf.Clamp01(currentAE / requiredAE)
            : (maxAE > 0f ? Mathf.Clamp01(currentAE / maxAE) : 0f);
        SetOverlayFill(ultAEOverlay, aeRatio, active: true);

        if (lastObservedUltimateAE < 0f)
            lastObservedUltimateAE = currentAE;

        // ── Cooldown Overlay — independent of AE ────────────────────────────
        if (!ultRush.isRushing)
        {
            if (cooldownBlocked)
            {
                // 1 → 0  (drains as cooldown ticks)
                float cdMax   = Mathf.Max(remainingCooldown, resolvedCooldown);
                float cdRatio = cdMax > 0f ? Mathf.Clamp01(remainingCooldown / cdMax) : 0f;
                SetOverlayFill(ultCooldownOverlay, cdRatio, active: true);
                SetIconAlpha(ultIcon, AlphaDimmed);
                SetCountdownText(ultCountdownText, remainingCooldown);
            }
            else
            {
                SetOverlayFill(ultCooldownOverlay, 0f, active: false);
                // Icon alpha: dim if AE not ready, bright if ready
                SetIconAlpha(ultIcon, isReady ? AlphaReady : AlphaDimmed);
                SetTextActive(ultCountdownText, false);

                if (isReady && !ultimateWasReadyLastFrame)
                    PlayAbilityReadyFeedback(AbilityType.Ultimate);
            }
        }
        else
        {
            // Rush is running — TickRush owns the overlay / icon alpha
            SetIconAlpha(ultIcon, isReady ? AlphaReady : AlphaDimmed);
            SetTextActive(ultCountdownText, false);
        }

        ultimateWasReadyLastFrame = isReady && !ultRush.isRushing;

        // ── Follow-Up Overlay ────────────────────────────────────────────────
        UpdateFollowUpOverlay(AttackCategory.Ultimate, ultFollowUpOverlay,
            ref ultimateFollowUpWasActiveLastFrame, AbilityType.Ultimate);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Follow-Up Overlay (shared for Skill + Ultimate)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the follow-up overlay (fill 1→0) whenever a passive availability
    /// window is active, regardless of ground/air state.
    /// Fires the ready feedback on the transition into the window.
    /// </summary>
    private void UpdateFollowUpOverlay(
        AttackCategory category,
        Image overlay,
        ref bool wasActiveLastFrame,
        AbilityType feedbackType)
    {
        FollowUpWindowState window = GetFollowUpWindowState(category);

        if (window.active)
        {
            // fill 1 → 0 as window drains
            float ratio = Mathf.Clamp01(window.remaining / window.duration);
            SetOverlayFill(overlay, ratio, active: true);

            if (!wasActiveLastFrame)
                PlayAbilityReadyFeedback(feedbackType);
        }
        else
        {
            SetOverlayFill(overlay, 0f, active: false);
        }

        wasActiveLastFrame = window.active;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private CombatAction ResolveDisplayedAction(AttackCategory category)
    {
        if (playerCharacter == null
            || !playerCharacter.TryResolveActionForContext(category, out CombatAction action, out _))
            return null;
        return action;
    }

    /// <summary>
    /// Resolves the best action for display-only purposes, ignoring the current
    /// ground/air context. Used so that cooldown text and overlays stay visible
    /// even when the player is airborne and the ability is ground-only.
    /// Prefers whichever action currently has an active cooldown; falls back to
    /// the first action in the table.
    /// </summary>
    private CombatAction ResolveDisplayedActionContextFree(AttackCategory category)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null
            || playerCharacter.characterObject.actionResolverTable == null)
            return null;

        ActionResolverTable table = playerCharacter.characterObject.actionResolverTable;

        System.Collections.Generic.IReadOnlyList<CombatAction> list = category switch
        {
            AttackCategory.Skill    => (System.Collections.Generic.IReadOnlyList<CombatAction>)table.skillActions,
            AttackCategory.Ultimate => table.ultimateActions,
            _                       => null
        };

        if (list == null || list.Count == 0) return null;

        // Prefer an action that currently has an active cooldown
        if (playerCharacter.Combat != null)
        {
            foreach (CombatAction action in list)
            {
                if (action == null) continue;
                if (action is SkillAction sa && sa.cooldown > 0f
                    && playerCharacter.Combat.IsOnCooldown(category, sa.cooldown, sa.actionName, sa.actionId))
                    return action;
                if (action is UltimateAction ua && ua.cooldown > 0f
                    && playerCharacter.Combat.IsOnCooldown(category, ua.cooldown, ua.actionName, ua.actionId))
                    return action;
            }
        }

        // Fall back to the first non-null entry
        foreach (CombatAction action in list)
            if (action != null) return action;

        return null;
    }

    private FollowUpWindowState GetFollowUpWindowState(AttackCategory category)
    {
        FollowUpWindowState state = default;
        if (playerCharacter == null
            || playerCharacter.PassiveController == null
            || !playerCharacter.TryResolveActionForContext(
                category,
                out CombatAction action,
                out _,
                requireAvailability: true)
            || action == null)
            return state;

        if (!playerCharacter.PassiveController.TryGetActionAvailabilityWindow(
                category,
                action.actionId,
                action.actionName,
                out float remainingSeconds,
                out float durationSeconds))
            return state;

        if (remainingSeconds <= 0.01f || durationSeconds <= 0.01f)
            return state;

        state.active    = true;
        state.remaining = remainingSeconds;
        state.duration  = durationSeconds;
        return state;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Primitive Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void SetOverlayFill(Image overlay, float fill, bool active)
    {
        if (overlay == null) return;
        overlay.gameObject.SetActive(active);
        if (active) overlay.fillAmount = fill;
    }

    private static void SetIconAlpha(Image icon, float alpha)
    {
        if (icon == null) return;
        Color c = icon.color;
        c.a       = alpha;
        icon.color = c;
    }

    private static void SetCountdownText(TMP_Text text, float seconds)
    {
        if (text == null) return;
        text.gameObject.SetActive(seconds > 0f);
        text.text = seconds > 0f ? FormatCooldownText(seconds) : string.Empty;
    }

    private static void SetTextActive(TMP_Text text, bool active)
    {
        if (text == null) return;
        text.gameObject.SetActive(active);
        if (!active) text.text = string.Empty;
    }

    /// <summary>Fully resets a slot to its default (inactive) state.</summary>
    private static void ResetSlot(Image cooldownOverlay, TMP_Text countdownText, Image followUpOverlay, Image icon)
    {
        SetOverlayFill(cooldownOverlay,  0f, active: false);
        SetOverlayFill(followUpOverlay,  0f, active: false);
        SetTextActive(countdownText, false);
        SetIconAlpha(icon, AlphaReady);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feedback
    // ─────────────────────────────────────────────────────────────────────────

    private void PlayAbilityReadyFeedback(AbilityType type)
    {
        if (!playFallbackReadyFeedback) return;

        RectTransform targetRect = ResolveFeedbackRoot(type);
        if (targetRect == null) return;

        EnsureFeedbackDriver();
        if (feedbackDriver == null) return;

        feedbackDriver.PlayPulse(targetRect, readyPulseScale, readyFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, readyGlintColor, readyFeedbackDuration);
        feedbackDriver.PlayRadialBurst(targetRect, readyBurstColor, 8, 36f, readyFeedbackDuration);
    }

    private void PlayAuraEnergyGainFeedback()
    {
        float now = Time.unscaledTime;
        if (now - lastAuraGainFeedbackTime < auraGainFeedbackMinInterval) return;

        RectTransform targetRect = ResolveFeedbackRoot(AbilityType.Ultimate);
        if (targetRect == null) return;

        EnsureFeedbackDriver();
        if (feedbackDriver == null) return;

        lastAuraGainFeedbackTime = now;
        feedbackDriver.PlayPulse(targetRect, auraGainPulseScale, auraGainFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, auraGainGlintColor, auraGainFeedbackDuration);
    }

    private RectTransform ResolveFeedbackRoot(AbilityType type)
    {
        if (type == AbilityType.Skill)
        {
            if (skillFeedbackRoot != null) return skillFeedbackRoot;
            return skillCooldownOverlay != null ? skillCooldownOverlay.transform.parent as RectTransform : null;
        }
        else
        {
            if (ultFeedbackRoot != null) return ultFeedbackRoot;
            return ultCooldownOverlay != null ? ultCooldownOverlay.transform.parent as RectTransform : null;
        }
    }

    private void EnsureFeedbackDriver()
    {
        if (feedbackDriver == null)
            feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Player / TeamLoader Binding
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheReferences()
    {
        if (playerCharacter == null)
            playerCharacter = FindFirstObjectByType<PlayerCharacter>();

        if (teamLoader == null)
        {
            teamLoader = FindFirstObjectByType<TeamLoader>();
            if (teamLoader != null)
            {
                teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;
                teamLoader.OnCharacterAuraEnergyUpdated += HandleCharacterAuraEnergyUpdated;
            }
        }
    }

    private void HandleCharacterAuraEnergyUpdated(CharacterObject character, float currentAE, float maxAE)
    {
        if (playerCharacter != null && playerCharacter.characterObject == character)
        {
            bool gained = lastObservedUltimateAE >= 0f && currentAE > lastObservedUltimateAE + 0.01f;
            lastObservedUltimateAE = currentAE;
            RefreshActiveCharacterUI();

            bool readyNow = currentAE >= maxAE;
            if (gained && !readyNow)
                PlayAuraEnergyGainFeedback();
        }
    }

    private void RebindPlayer()
    {
        if (playerCharacter == subscribedPlayer) return;

        UnbindPlayer();
        subscribedPlayer = playerCharacter;

        if (subscribedPlayer != null)
            subscribedPlayer.OnCharacterChanged += HandleCharacterChanged;
    }

    private void UnbindPlayer()
    {
        if (subscribedPlayer == null) return;
        subscribedPlayer.OnCharacterChanged -= HandleCharacterChanged;
        subscribedPlayer = null;
    }

    private void HandleCharacterChanged(CharacterObject newCharacter)
    {
        displayedCharacterKey              = BuildCharacterKey(newCharacter);
        ultimateWasReadyLastFrame          = false;
        skillWasReadyLastFrame             = false;
        skillFollowUpWasActiveLastFrame    = false;
        ultimateFollowUpWasActiveLastFrame = false;
        skillRush                          = default;
        ultRush                            = default;
        lastObservedUltimateAE             = GetCurrentAuraEnergy();
        RefreshActiveCharacterUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────────

    private string GetCurrentCharacterKey()
    {
        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        return BuildCharacterKey(currentCharacter);
    }

    private static string BuildCharacterKey(CharacterObject character)
    {
        return character == null ? string.Empty : character.GetInstanceID().ToString();
    }

    private float GetCurrentAuraEnergy()
    {
        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        return teamLoader != null && currentCharacter != null
            ? teamLoader.GetCharacterCurrentAE(currentCharacter)
            : -1f;
    }

    private static string FormatCooldownText(float seconds)
    {
        return seconds < 1f
            ? seconds.ToString("0.0")
            : Mathf.CeilToInt(seconds).ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gameplay UI Fading
    // ─────────────────────────────────────────────────────────────────────────
    private static Coroutine _fadeCoroutine;

    public static void FadeGameplayUI(float targetAlpha, float duration)
    {
        if (Instance == null) return;

        if (_fadeCoroutine != null)
        {
            Instance.StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _fadeCoroutine = Instance.StartCoroutine(FadeGameplayUICoroutine(targetAlpha, duration));
    }

    private static System.Collections.IEnumerator FadeGameplayUICoroutine(float targetAlpha, float duration)
    {
        List<CanvasGroup> canvasGroups = new List<CanvasGroup>();

        // 1. AbilityUIManager Canvas Group
        CanvasGroup abilityCG = Instance.GetComponentInParent<CanvasGroup>();
        if (abilityCG == null && Instance.transform.parent != null)
        {
            Canvas canvas = Instance.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                abilityCG = canvas.GetComponent<CanvasGroup>();
                if (abilityCG == null)
                {
                    abilityCG = canvas.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
        if (abilityCG != null) canvasGroups.Add(abilityCG);

        // 2. PlayerHUD Canvas Groups
        PlayerHUD[] huds = Object.FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hud in huds)
        {
            CanvasGroup hudCG = hud.GetComponentInParent<CanvasGroup>();
            if (hudCG == null)
            {
                Canvas canvas = hud.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    hudCG = canvas.GetComponent<CanvasGroup>();
                    if (hudCG == null)
                    {
                        hudCG = canvas.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }
            if (hudCG != null && !canvasGroups.Contains(hudCG))
            {
                canvasGroups.Add(hudCG);
            }
        }

        if (canvasGroups.Count == 0) yield break;

        float startAlpha = canvasGroups[0].alpha;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            foreach (var cg in canvasGroups)
            {
                cg.alpha = targetAlpha;
                cg.interactable = targetAlpha > 0.01f;
                cg.blocksRaycasts = targetAlpha > 0.01f;
            }
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                foreach (var cg in canvasGroups)
                {
                    cg.alpha = currentAlpha;
                }
                yield return null;
            }

            foreach (var cg in canvasGroups)
            {
                cg.alpha = targetAlpha;
                cg.interactable = targetAlpha > 0.01f;
                cg.blocksRaycasts = targetAlpha > 0.01f;
            }
        }

        _fadeCoroutine = null;
    }
}
