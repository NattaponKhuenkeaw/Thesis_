
using UnityEngine;
using UnityEngine.UI;
using Project.Progression;

public class AdminConsole : MonoBehaviour
{

    [SerializeField] private GameObject enemyPrefab;

    [Header("Reset Systems")]
    [SerializeField] private Project.ShopSystem.PlayerRunInventory runInventory;
    [SerializeField] private Project.Progression.PlayerMetaProgression progressionInventory;

    public void TeleportPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = new Vector2(0,0);
    }

    public void SpawnEnemy()
    {
        Instantiate(enemyPrefab, new Vector3(Random.Range(-10, 10), 0, 0), Quaternion.identity);
    }

    public void KillAllEnemies()
    {
        Destroy(GameObject.FindGameObjectWithTag("Enemy"));
    }

    public void KillPlayer()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerCharacter>();
        }

        if (player == null)
        {
            Debug.LogWarning("[AdminConsole] Cannot kill player because no PlayerCharacter was found.");
            return;
        }

        player.ForceDeath();
        Debug.Log("[AdminConsole] Player death triggered.");
    }

    public void HealPlayer()
    {
        
    }

    public void ResetLevel()
    {
        
    }

    /// <summary>
    /// Unity OnClick prototype for the future Level-room reward sequence.
    /// </summary>
    public void TriggerGlobalLevelUpEvent()
    {
        if (runInventory == null)
        {
            Debug.LogWarning("[AdminConsole] Run Global Level event requires a PlayerRunInventory reference.");
            return;
        }

        int rolledLevels = RunGlobalLevelReward.Roll();
        int previousLevel = runInventory.GlobalLevel;
        int appliedLevels = runInventory.AddGlobalLevels(rolledLevels);

        Debug.Log(
            $"[AdminConsole] Run Global Level event rolled +{rolledLevels}, applied +{appliedLevels}, "
            + $"and changed Global Lv.{previousLevel} to Lv.{runInventory.GlobalLevel}.");
    }

    public void ResetPlayerRun()
    {
        if (runInventory != null)
        {
            runInventory.ResetRunProgress();
        }
        if (progressionInventory != null)
        {
            progressionInventory.ClearAllProgression();
        }
        Debug.Log("[AdminConsole] Player Run Progress and Meta Progression have been reset.");
    }

    public void ToggleTrainingDebug()
    {
        if (CombatCore.TrainingDebugManager.Instance != null)
        {
            CombatCore.TrainingDebugManager.Instance.ToggleDebugMode();
        }
    }

    public void ClearState()
    {
        if (Project.Core.ZoneState.SceneTransitionManager.Instance == null
            || !Project.Core.ZoneState.SceneTransitionManager.Instance.SetCurrentRoomExitAvailable(true))
        {
            Debug.LogWarning("[AdminConsole] Current scene exit could not be unlocked.");
        }
    }

}
