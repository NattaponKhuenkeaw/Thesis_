using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityUIManager : MonoBehaviour
{
    public static AbilityUIManager Instance;

    public enum AbilityType { Skill, Ultimate }

    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Skill UI")]
    [SerializeField] private RectTransform skillReadyFeedbackRoot;
    public Image skillCooldownFill;
    public TMP_Text skillCooldownText;

    [Header("Ultimate UI")]
    [SerializeField] private RectTransform ultReadyFeedbackRoot;
    public Image ultCooldownFill;
    public TMP_Text ultCooldownText;

    [Header("Fallback Ready Feedback")]
    [SerializeField] private bool playFallbackReadyFeedback = true;
    [SerializeField] private Color readyGlintColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color readyBurstColor = new Color(0.6f, 1f, 0.85f, 0.9f);
    [SerializeField] private float readyPulseScale = 1.25f;
    [SerializeField] private float readyFeedbackDuration = 0.18f;

    [Header("Fallback Rejected Feedback")]
    [SerializeField] private Color rejectedFlashColor = new Color(1f, 0.2f, 0.15f, 0.9f);
    [SerializeField] private float rejectedWiggleDistance = 8f;
    [SerializeField] private float rejectedFeedbackDuration = 0.18f;

    [Header("Fallback Aura Gain Feedback")]
    [SerializeField] private Color auraGainGlintColor = new Color(0.45f, 1f, 0.75f, 0.55f);
    [SerializeField] private float auraGainPulseScale = 1.04f;
    [SerializeField] private float auraGainFeedbackDuration = 0.1f;
    [SerializeField] private float auraGainFeedbackMinInterval = 0.08f;

    private struct CooldownDisplayState
    {
        public float current;
        public float max;
    }

    private readonly Dictionary<string, CooldownDisplayState> skillCooldowns = new Dictionary<string, CooldownDisplayState>();
    private readonly Dictionary<string, CooldownDisplayState> ultimateCooldowns = new Dictionary<string, CooldownDisplayState>();

    private PlayerCharacter subscribedPlayer;
    private string displayedCharacterKey = string.Empty;
    private UIFeedbackDriver feedbackDriver;
    private TeamLoader teamLoader;
    private float lastObservedUltimateAE = -1f;
    private float lastAuraGainFeedbackTime = -999f;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        CacheReferences();
        feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);
        ResetUI(skillCooldownFill, skillCooldownText);
        ResetUI(ultCooldownFill, ultCooldownText);
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
        {
            teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;
        }
    }

    private bool ultimateWasReadyLastFrame = false;

    private void Update()
    {
        TickCooldownDictionary(skillCooldowns, AbilityType.Skill);
        TickCooldownDictionary(ultimateCooldowns, AbilityType.Ultimate);

        CacheReferences();
        RebindPlayer();

        string currentCharacterKey = GetCurrentCharacterKey();
        if (currentCharacterKey != displayedCharacterKey)
        {
            displayedCharacterKey = currentCharacterKey;
            ultimateWasReadyLastFrame = false;
            lastObservedUltimateAE = GetCurrentAuraEnergy();
        }

        RefreshActiveCharacterUI();
    }

    public void TriggerCooldown(AbilityType type, float duration)
    {
        if (duration <= 0f)
        {
            RefreshActiveCharacterUI();
            return;
        }

        string characterKey = GetCurrentCharacterKey();
        if (string.IsNullOrEmpty(characterKey))
            return;

        Dictionary<string, CooldownDisplayState> targetDictionary =
            type == AbilityType.Skill ? skillCooldowns : ultimateCooldowns;

        targetDictionary[characterKey] = new CooldownDisplayState
        {
            current = duration,
            max = duration
        };

        RefreshActiveCharacterUI();
    }

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
            {
                PlayAuraEnergyGainFeedback();
            }
        }
    }

    private void RebindPlayer()
    {
        if (playerCharacter == subscribedPlayer)
            return;

        UnbindPlayer();
        subscribedPlayer = playerCharacter;

        if (subscribedPlayer != null)
            subscribedPlayer.OnCharacterChanged += HandleCharacterChanged;
    }

    private void UnbindPlayer()
    {
        if (subscribedPlayer == null)
            return;

        subscribedPlayer.OnCharacterChanged -= HandleCharacterChanged;
        subscribedPlayer = null;
    }

    private void HandleCharacterChanged(CharacterObject newCharacter)
    {
        displayedCharacterKey = BuildCharacterKey(newCharacter);
        ultimateWasReadyLastFrame = false;
        lastObservedUltimateAE = GetCurrentAuraEnergy();
        RefreshActiveCharacterUI();
    }

    private void TickCooldownDictionary(Dictionary<string, CooldownDisplayState> cooldowns, AbilityType type)
    {
        if (cooldowns.Count == 0)
            return;

        List<string> keysToClear = null;
        List<string> keys = new List<string>(cooldowns.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            CooldownDisplayState state = cooldowns[key];
            state.current = Mathf.Max(0f, state.current - Time.deltaTime);

            if (state.current <= 0f || state.max <= 0f)
            {
                keysToClear ??= new List<string>();
                keysToClear.Add(key);

                string currentCharacterKey = GetCurrentCharacterKey();
                if (string.Equals(key, currentCharacterKey, System.StringComparison.Ordinal))
                {
                    PlayAbilityReadyFeedback(type);
                }

                continue;
            }

            cooldowns[key] = state;
        }

        if (keysToClear == null)
            return;

        for (int i = 0; i < keysToClear.Count; i++)
        {
            cooldowns.Remove(keysToClear[i]);
        }
    }

    private void RefreshActiveCharacterUI()
    {
        string characterKey = GetCurrentCharacterKey();
        displayedCharacterKey = characterKey;

        RefreshAbilityUI(skillCooldowns, characterKey, skillCooldownFill, skillCooldownText);
        UpdateUltimateEnergyUI();
    }

    private void UpdateUltimateEnergyUI()
    {
        if (ultCooldownFill == null && ultCooldownText == null)
            return;

        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        if (currentCharacter == null)
        {
            ResetUI(ultCooldownFill, ultCooldownText);
            ultimateWasReadyLastFrame = false;
            lastObservedUltimateAE = -1f;
            return;
        }

        if (teamLoader == null)
        {
            ResetUI(ultCooldownFill, ultCooldownText);
            ultimateWasReadyLastFrame = false;
            lastObservedUltimateAE = -1f;
            return;
        }

        float currentAE = teamLoader.GetCharacterCurrentAE(currentCharacter);
        float maxAE = teamLoader.GetCharacterMaxAE(currentCharacter);
        float fillRatio = maxAE > 0f ? Mathf.Clamp01(currentAE / maxAE) : 0f;

        bool isReady = maxAE > 0f && currentAE >= maxAE;

        if (isReady)
        {
            ResetUI(ultCooldownFill, ultCooldownText);

            if (!ultimateWasReadyLastFrame)
            {
                PlayAbilityReadyFeedback(AbilityType.Ultimate);
            }
        }
        else
        {
            if (ultCooldownFill != null)
            {
                ultCooldownFill.gameObject.SetActive(true);
                ultCooldownFill.fillAmount = 1f - fillRatio;
            }

            if (ultCooldownText != null)
            {
                ultCooldownText.gameObject.SetActive(false);
                ultCooldownText.text = string.Empty;
            }
        }

        if (lastObservedUltimateAE < 0f)
            lastObservedUltimateAE = currentAE;

        ultimateWasReadyLastFrame = isReady;
    }

    private void RefreshAbilityUI(
        Dictionary<string, CooldownDisplayState> cooldowns,
        string characterKey,
        Image fillImage,
        TMP_Text text)
    {
        if (string.IsNullOrEmpty(characterKey) || !cooldowns.TryGetValue(characterKey, out CooldownDisplayState state))
        {
            ResetUI(fillImage, text);
            return;
        }

        UpdateUI(state.current, state.max, fillImage, text);
    }

    private string GetCurrentCharacterKey()
    {
        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        return BuildCharacterKey(currentCharacter);
    }

    private string BuildCharacterKey(CharacterObject character)
    {
        if (character == null)
            return string.Empty;

        return character.GetInstanceID().ToString();
    }

    private void UpdateUI(float current, float max, Image fillImage, TMP_Text text)
    {
        if (fillImage == null || text == null)
            return;

        if (current <= 0f || max <= 0f)
        {
            ResetUI(fillImage, text);
            return;
        }

        fillImage.gameObject.SetActive(true);
        text.gameObject.SetActive(true);
        fillImage.fillAmount = Mathf.Clamp01(current / max);
        text.text = FormatCooldownText(current);
    }

    private void ResetUI(Image fillImage, TMP_Text text)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(false);
        }

        if (text != null)
        {
            text.gameObject.SetActive(false);
            text.text = string.Empty;
        }
    }

    private void PlayAbilityReadyFeedback(AbilityType type)
    {
        if (!playFallbackReadyFeedback)
        {
            return;
        }

        Image targetFill = type == AbilityType.Skill ? skillCooldownFill : ultCooldownFill;
        RectTransform targetRect = type == AbilityType.Skill ? skillReadyFeedbackRoot : ultReadyFeedbackRoot;
        if (targetRect == null && targetFill != null)
        {
            Transform feedbackParent = targetFill.transform.parent != null
                ? targetFill.transform.parent
                : targetFill.transform;
            targetRect = feedbackParent as RectTransform;
        }

        if (targetRect == null)
        {
            return;
        }

        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
        if (feedbackDriver == null)
        {
            return;
        }

        feedbackDriver.PlayPulse(targetRect, readyPulseScale, readyFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, readyGlintColor, readyFeedbackDuration);
        feedbackDriver.PlayRadialBurst(targetRect, readyBurstColor, 8, 36f, readyFeedbackDuration);
    }

    public void PlayAbilityRejectedFeedback(AbilityType type)
    {
        RectTransform targetRect = ResolveFeedbackRoot(type);
        if (targetRect == null)
        {
            return;
        }

        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
        if (feedbackDriver == null)
        {
            return;
        }

        Vector2 origin = targetRect.anchoredPosition;
        feedbackDriver.PlayWiggle(targetRect, origin, rejectedWiggleDistance, rejectedFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, rejectedFlashColor, rejectedFeedbackDuration);
    }

    private void PlayAuraEnergyGainFeedback()
    {
        float now = Time.unscaledTime;
        if (now - lastAuraGainFeedbackTime < auraGainFeedbackMinInterval)
        {
            return;
        }

        RectTransform targetRect = ResolveFeedbackRoot(AbilityType.Ultimate);
        if (targetRect == null)
        {
            return;
        }

        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
        if (feedbackDriver == null)
        {
            return;
        }

        lastAuraGainFeedbackTime = now;
        feedbackDriver.PlayPulse(targetRect, auraGainPulseScale, auraGainFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, auraGainGlintColor, auraGainFeedbackDuration);
    }

    private RectTransform ResolveFeedbackRoot(AbilityType type)
    {
        Image targetFill = type == AbilityType.Skill ? skillCooldownFill : ultCooldownFill;
        RectTransform targetRect = type == AbilityType.Skill ? skillReadyFeedbackRoot : ultReadyFeedbackRoot;
        if (targetRect == null && targetFill != null)
        {
            Transform feedbackParent = targetFill.transform.parent != null
                ? targetFill.transform.parent
                : targetFill.transform;
            targetRect = feedbackParent as RectTransform;
        }

        return targetRect;
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
}
