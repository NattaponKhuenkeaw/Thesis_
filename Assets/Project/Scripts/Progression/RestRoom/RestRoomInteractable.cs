using Project.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Project.Progression.RestRoom
{
    /// <summary>
    /// Approach zone that selects the Rest display and confirms the heal event.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class RestRoomInteractable : MonoBehaviour
    {
        private static readonly int IsSelectedHash = Animator.StringToHash("IsSelected");
        private static readonly int IsIdleHash = Animator.StringToHash("IsIdle");

        [SerializeField] private RestRoomController controller;
        [SerializeField] private Animator buttonAnimator;
        [SerializeField] private RestRoomDisplay display;
        [SerializeField] private SpriteRenderer buttonVisual;
        [SerializeField] private string interactKey = "e";
        [SerializeField] private bool alsoAcceptEnter = true;
        [SerializeField] private Color idleTint = new Color(0.75f, 0.78f, 0.9f, 1f);
        [SerializeField] private Color selectedTint = new Color(0.45f, 0.95f, 0.75f, 1f);

        private bool playerInRange;
        private Vector3 idleScale = Vector3.one;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<RestRoomController>();
            }

            if (buttonAnimator == null)
            {
                buttonAnimator = GetComponent<Animator>();
            }

            if (buttonVisual == null)
            {
                buttonVisual = GetComponent<SpriteRenderer>();
                if (buttonVisual == null && display != null)
                {
                    buttonVisual = display.GetComponent<SpriteRenderer>();
                }
            }

            idleScale = buttonVisual != null ? buttonVisual.transform.localScale : transform.localScale;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            ApplyIdleVisual();
        }

        private void Update()
        {
            if (!playerInRange
                || controller == null
                || controller.IsSequenceActive
                || InputLockManager.IsLocked)
            {
                return;
            }

            if (WasInteractPressed())
            {
                controller.TryBeginSequence();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player") || controller == null)
            {
                return;
            }

            playerInRange = true;
            if (controller.IsSequenceActive)
            {
                return;
            }

            if (controller.HasClaimedThisVisit)
            {
                display?.ShowClaimed();
                ApplyIdleVisual();
                return;
            }

            ApplySelectedVisual();
            display?.ShowSelected();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            playerInRange = false;
            if (controller != null && controller.IsSequenceActive)
            {
                return;
            }

            ApplyIdleVisual();
            if (controller != null && controller.HasClaimedThisVisit)
            {
                display?.ShowClaimed();
            }
            else
            {
                display?.ShowIdle();
            }
        }

        public void ForceIdleVisual()
        {
            ApplyIdleVisual();
        }

        private bool WasInteractPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (alsoAcceptEnter
                    && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(interactKey))
                {
                    try
                    {
                        InputControl control = keyboard[interactKey.ToLowerInvariant()];
                        if (control is KeyControl key && key.wasPressedThisFrame)
                        {
                            return true;
                        }
                    }
                    catch (System.ArgumentException)
                    {
                        // Invalid key strings are ignored.
                    }
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null
                && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        private void ApplySelectedVisual()
        {
            if (buttonAnimator != null)
            {
                buttonAnimator.ResetTrigger(IsIdleHash);
                buttonAnimator.SetTrigger(IsSelectedHash);
            }

            if (buttonVisual != null)
            {
                buttonVisual.color = selectedTint;
                buttonVisual.transform.localScale = idleScale * 1.06f;
            }
        }

        private void ApplyIdleVisual()
        {
            if (buttonAnimator != null)
            {
                buttonAnimator.ResetTrigger(IsSelectedHash);
                buttonAnimator.SetTrigger(IsIdleHash);
            }

            if (buttonVisual != null)
            {
                buttonVisual.color = idleTint;
                buttonVisual.transform.localScale = idleScale;
            }
        }
    }
}
