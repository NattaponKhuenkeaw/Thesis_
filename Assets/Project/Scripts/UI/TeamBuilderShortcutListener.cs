using UnityEngine;
using UnityEngine.InputSystem;
using Project.TeamBuilder.UI;

namespace Project.UI
{
    public class TeamBuilderShortcutListener : MonoBehaviour
    {
        [SerializeField] private TeamBuilderUI teamBuilderUI;
        [SerializeField] private Key toggleKey = Key.Tab;

        private void Update()
        {
            if (PauseMenuController.IsOpen)
            {
                return;
            }

            if (Keyboard.current != null
                && toggleKey != Key.None
                && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (teamBuilderUI != null)
                {
                    teamBuilderUI.TogglePanel();
                }
            }
        }
    }
}
