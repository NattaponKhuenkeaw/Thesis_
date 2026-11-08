using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.TeamBuilder.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ArtifactCopyMenuUI : MonoBehaviour
    {
        private const string SkipDiscardConfirmation = "project.artifact.skipDiscardConfirmation";
        public const string MenuPath = "UI/ArtifactCopyMenu";
        public const string DialogPath = "UI/ArtifactDiscardDialog";
        private static ArtifactCopyMenuUI active;
        private CanvasGroup group;
        private Transform windowTransform;
        private GameObject previousFocus;
        private bool closing;
        private bool closedNotified;

        public event Action<ArtifactCopyMenuUI> Closed;

        public static bool SkipConfirmation => PlayerPrefs.GetInt(SkipDiscardConfirmation, 0) == 1;

        public static bool TryDismissActive()
        {
            if (active == null || !active.isActiveAndEnabled) return false;
            active.Close();
            return true;
        }

        public void OpenMenu(bool favourite, bool canDiscard, Vector2 screenPosition, Camera eventCamera,
            Action onFavourite, Action onDiscard)
        {
            RectTransform canvas = (RectTransform)transform;
            RectTransform window = (RectTransform)transform.Find("Window");
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPosition, eventCamera, out Vector2 point))
            {
                Vector2 half = window.rect.size * .5f;
                Rect bounds = canvas.rect;
                window.anchoredPosition = new Vector2(
                    Mathf.Clamp(point.x + half.x, bounds.xMin + half.x, bounds.xMax - half.x),
                    Mathf.Clamp(point.y - half.y, bounds.yMin + half.y, bounds.yMax - half.y));
            }

            Button favouriteButton = FindButton("Window/Favourite");
            favouriteButton.GetComponentInChildren<TMP_Text>().text = favourite ? "Unfavourite" : "Favourite";
            favouriteButton.onClick.AddListener(() => Close(onFavourite));
            Button discard = FindButton("Window/Discard");
            discard.interactable = canDiscard;
            discard.onClick.AddListener(() => Close(onDiscard));
            Open(favouriteButton);
        }

        public void OpenDiscard(string itemName, Func<bool> onDiscard)
        {
            transform.Find("Window/Title").GetComponent<TMP_Text>().text = $"Discard {itemName}?";
            Toggle skip = transform.Find("Window/Skip").GetComponent<Toggle>();
            Button cancel = FindButton("Window/Cancel");
            cancel.onClick.AddListener(() => Close());
            FindButton("Window/Discard").onClick.AddListener(() =>
            {
                if (closing) return;
                bool skipConfirmation = skip.isOn;
                // Finish closing before inventory events rebuild or disable the originating grid.
                previousFocus = null;
                Close(() =>
                {
                    if (onDiscard() && skipConfirmation)
                    {
                        PlayerPrefs.SetInt(SkipDiscardConfirmation, 1);
                        PlayerPrefs.Save();
                    }
                });
            });
            Open(cancel);
        }

        private Button FindButton(string path) => transform.Find(path).GetComponent<Button>();

        private void Open(Button initialFocus)
        {
            group = GetComponent<CanvasGroup>();
            closing = false;
            closedNotified = false;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.alpha = 0f;
            windowTransform = transform.Find("Window");
            active = this;
            previousFocus = EventSystem.current?.currentSelectedGameObject;
            Button dismiss = FindButton("Dismiss");
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            dismiss.onClick.AddListener(() => Close());
            EventSystem.current?.SetSelectedGameObject(initialFocus.gameObject);
            StartCoroutine(Animate(1f));
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true)
                Close();
        }

        private void OnDisable()
        {
            if (active == this) active = null;
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            NotifyClosed();
        }

        private void NotifyClosed()
        {
            if (closedNotified) return;
            closedNotified = true;
            Closed?.Invoke(this);
        }

        private void Close(Action then = null)
        {
            if (closing || group == null) return;
            closing = true;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (active == this) active = null;
            NotifyClosed();
            StopAllCoroutines();
            StartCoroutine(CloseRoutine(then));
        }

        private IEnumerator CloseRoutine(Action then)
        {
            yield return Animate(0f);
            gameObject.SetActive(false);
            if (previousFocus != null && previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(previousFocus);
            try { then?.Invoke(); }
            finally { Destroy(gameObject); }
        }

        private IEnumerator Animate(float target)
        {
            const float duration = .14f;
            float start = group.alpha;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, elapsed / duration);
                windowTransform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, group.alpha);
                yield return null;
            }
            group.alpha = target;
            windowTransform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, target);
        }
    }
}
