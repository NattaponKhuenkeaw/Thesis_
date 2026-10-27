using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIFeedbackDriver))]
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] backgroundGroups;
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private Outline logoGlow;
        [SerializeField] private CanvasGroup[] buttonGroups;
        [Header("Intro Timing")]
        [SerializeField, Min(0f)] private float backgroundFade = .3f;
        [SerializeField, Min(0f)] private float logoDelay = .08f;
        [SerializeField, Min(0f)] private float logoFade = .28f;
        [SerializeField, Min(0f)] private float buttonsDelay = .3f;
        [SerializeField, Min(0f)] private float buttonFade = .2f;
        [SerializeField, Min(0f)] private float buttonStagger = .05f;
        [SerializeField, Min(.01f)] private float logoGlowDuration = .18f;
        private readonly List<Coroutine> fades = new List<Coroutine>();
        private UIFeedbackDriver driver;
        private Coroutine intro;
        private Coroutine glow;
        private bool completed;
        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            driver = GetComponent<UIFeedbackDriver>();
            SetAlphas(0f);
            SetGlow(0f);
        }

        private void Start() { if (!IsPlaying && !completed) PlayIntro(); }

        [ContextMenu("Replay Intro (Play Mode)")]
        public void PlayIntro()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            CompleteIntro();
            completed = false;
            IsPlaying = true;
            SetAlphas(0f);
            intro = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            if (backgroundGroups != null)
                foreach (var group in backgroundGroups) Fade(group, backgroundFade);
            yield return new WaitForSecondsRealtime(logoDelay);
            Fade(logoGroup, logoFade);
            glow = StartCoroutine(Glow());
            yield return new WaitForSecondsRealtime(Mathf.Max(0, buttonsDelay - logoDelay));
            if (buttonGroups != null)
                for (int i = 0; i < buttonGroups.Length; i++)
                {
                    Fade(buttonGroups[i], buttonFade);
                    if (i < buttonGroups.Length - 1) yield return new WaitForSecondsRealtime(buttonStagger);
                }
            yield return new WaitForSecondsRealtime(Mathf.Max(buttonFade, logoDelay + logoFade + logoGlowDuration - buttonsDelay));
            intro = null;
            CompleteIntro();
        }

        private void Fade(CanvasGroup group, float duration)
        {
            if (group != null) fades.Add(driver.PlayFade(group, true, duration));
        }

        private IEnumerator Glow()
        {
            yield return new WaitForSecondsRealtime(logoFade);
            for (float elapsed = 0; elapsed < logoGlowDuration; elapsed += Time.unscaledDeltaTime)
            {
                SetGlow(Mathf.Sin(Mathf.Clamp01(elapsed / logoGlowDuration) * Mathf.PI) * .24f);
                yield return null;
            }
            SetGlow(0);
            glow = null;
        }

        public void CompleteIntro()
        {
            if (intro != null) StopCoroutine(intro);
            if (glow != null) StopCoroutine(glow);
            intro = glow = null;
            if (driver != null) foreach (var fade in fades) if (fade != null) driver.StopCoroutine(fade);
            fades.Clear();
            IsPlaying = false;
            completed = true;
            SetAlphas(1f);
            SetGlow(0f);
        }

        private void SetAlphas(float alpha)
        {
            if (backgroundGroups != null) foreach (var group in backgroundGroups) if (group != null) group.alpha = alpha;
            if (logoGroup != null) logoGroup.alpha = alpha;
            if (buttonGroups != null) foreach (var group in buttonGroups) if (group != null) group.alpha = alpha;
        }

        private void SetGlow(float alpha)
        {
            if (logoGlow == null) return;
            logoGlow.effectColor = new Color(1f, .92f, .2f, alpha);
            logoGlow.enabled = alpha > .001f;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame)))
                CompleteIntro();
        }

        private void OnDisable() => CompleteIntro();
    }
}
