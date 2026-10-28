using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIHoverFallbackFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float transitionDuration = .2f;
    [SerializeField] private bool playClickGlint = true;
    private UIFeedbackDriver driver;

    public void Configure(float scale, Color glow, float duration)
    {
        hoverScale = scale;
        transitionDuration = duration;
    }

    public void OnPointerEnter(PointerEventData data) => Play();
    public void OnPointerExit(PointerEventData data) { }
    public void OnPointerClick(PointerEventData data)
    {
        if (playClickGlint && data.button == PointerEventData.InputButton.Left) Play();
    }

    private void Play()
    {
        var selectable = GetComponent<Selectable>();
        if (!isActiveAndEnabled || (selectable != null && !selectable.IsInteractable())) return;
        driver = driver != null ? driver : UIFeedbackDriver.GetOrAdd(gameObject);
        driver.PlayPulse(transform, hoverScale, transitionDuration);
    }
}
