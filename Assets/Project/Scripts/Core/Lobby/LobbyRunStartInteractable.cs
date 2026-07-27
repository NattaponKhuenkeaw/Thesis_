using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Core.Lobby
{
    /// <summary>
    /// Lobby hook for starting a run from UI Button click and/or 2D trigger volume.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyRunStartInteractable : MonoBehaviour
    {
        [Header("Optional UI Feedback")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private string readyMessage = "Press to enter the dungeon.";
        [SerializeField] private string blockedMessage = "Deploy your team in the Team Builder first.";

        [Header("Trigger (Optional)")]
        [SerializeField] private bool startOnTriggerEnter;
        [SerializeField] private string playerTag = "Player";

        private GameRunManager runManager;

        private void Awake()
        {
            runManager = GameRunManager.Instance ?? FindFirstObjectByType<GameRunManager>();
            if (runManager != null)
            {
                runManager.OnRunStartFailed += HandleRunStartFailed;
            }

            if (statusText != null && !string.IsNullOrWhiteSpace(readyMessage))
            {
                statusText.text = readyMessage;
            }
        }

        private void OnDestroy()
        {
            if (runManager != null)
            {
                runManager.OnRunStartFailed -= HandleRunStartFailed;
            }
        }

        /// <summary>
        /// Wire this to a UI Button OnClick() in Test_Lobby.
        /// </summary>
        public void OnStartRunButtonClicked()
        {
            TryStartRun();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!startOnTriggerEnter || other == null)
            {
                return;
            }

            if (!other.CompareTag(playerTag))
            {
                return;
            }

            TryStartRun();
        }

        private void TryStartRun()
        {
            runManager ??= GameRunManager.Instance ?? FindFirstObjectByType<GameRunManager>();
            if (runManager == null)
            {
                HandleRunStartFailed("GameRunManager is missing from the persistent manager hierarchy.");
                return;
            }

            if (!runManager.TryStartNewRun(out string failureReason))
            {
                HandleRunStartFailed(failureReason);
            }
        }

        private void HandleRunStartFailed(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = string.IsNullOrWhiteSpace(message) ? blockedMessage : message;
        }
    }
}
