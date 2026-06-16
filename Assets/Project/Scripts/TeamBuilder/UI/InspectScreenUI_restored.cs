using System.Collections;
using UnityEngine;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI.Panels;

namespace Project.TeamBuilder.UI
{
    public class InspectScreenUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject contentPanel;

        [Header("Sub-Panels")]
        [SerializeField] private CharacterInfoPanel infoPanel;
        [SerializeField] private CharacterStatsPanel statsPanel;
        [SerializeField] private CharacterSkillPanel skillPanel;

        [Header("Animation")]
        [SerializeField] private float slideOffset = 480f;
        [SerializeField] private float slideDuration = 0.25f;

        private CharacterItem currentCharacter;
        private Artifact[] currentArtifacts = new Artifact[0];
        private RectTransform panelRectTransform;
        private CanvasGroup panelCanvasGroup;
        private Coroutine transitionCoroutine;
        private Vector2 openAnchoredPosition;

        private void Awake()
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            panelRectTransform = targetPanel.GetComponent<RectTransform>();
            panelCanvasGroup = targetPanel.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = targetPanel.AddComponent<CanvasGroup>();
            }

            if (panelRectTransform != null)
            {
                openAnchoredPosition = panelRectTransform.anchoredPosition;
            }

            targetPanel.SetActive(false);
            panelCanvasGroup.alpha = 0f;
        }

        public void Open(CharacterItem character, Artifact[] equippedArtifacts = null)
        {
            if (character == null || character.characterObjectData == null)
            {
                return;
            }

            currentCharacter = character;
            currentArtifacts = equippedArtifacts ?? new Artifact[0];
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            targetPanel.SetActive(true);

            PopulateData();
            PlayTransition(true);
        }

        public void Open(CharacterItem character)
        {
            Open(character, null);
        }

        public void Close()
        {
            bool isOpen = currentCharacter != null || ((contentPanel != null ? contentPanel : gameObject).activeSelf);
            if (!isOpen)
            {
                return;
            }

            PlayTransition(false);
        }

        private void PopulateData()
        {
            if (currentCharacter == null || currentCharacter.characterObjectData == null)
                return;

            CharacterObject data = currentCharacter.characterObjectData;

            if (infoPanel != null)
            {
                infoPanel.Populate(currentCharacter);
            }

            if (statsPanel != null)
            {
                CalculatedStats calculatedStats = CharacterStatCalculator.Calculate(data, currentArtifacts);
                statsPanel.Populate(calculatedStats);
            }

            if (skillPanel != null)
            {
                skillPanel.Populate(data, currentArtifacts);
            }
        }

        private void PlayTransition(bool opening)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(PlayTransitionRoutine(opening));
        }

        private IEnumerator PlayTransitionRoutine(bool opening)
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            if (panelRectTransform == null)
            {
                targetPanel.SetActive(opening);
                if (!opening)
                {
                    currentCharacter = null;
                    currentArtifacts = new Artifact[0];
                }
                yield break;
            }

            if (opening)
            {
                panelRectTransform.anchoredPosition = openAnchoredPosition + new Vector2(slideOffset, 0f);
            }

            float elapsed = 0f;
            Vector2 startPosition = panelRectTransform.anchoredPosition;
            Vector2 endPosition = opening ? openAnchoredPosition : openAnchoredPosition + new Vector2(slideOffset, 0f);
            float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }

                yield return null;
            }

            panelRectTransform.anchoredPosition = endPosition;
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = endAlpha;
            }

            if (!opening)
            {
                targetPanel.SetActive(false);
                currentCharacter = null;
                currentArtifacts = new Artifact[0];
            }

            transitionCoroutine = null;
        }
    }
}

