
using UnityEngine;
using UnityEngine.UI;
using Project.Progression;
using Project.UI;

[RequireComponent(typeof(UIBlockPlayerClick))]
public class AdminConsole : MonoBehaviour
{

    [SerializeField] private GameObject enemyPrefab;

    [Header("Reset Systems")]
    [SerializeField] private Project.ShopSystem.PlayerRunInventory runInventory;
    [SerializeField] private Project.Progression.PlayerMetaProgression progressionInventory;

    private void Awake()
    {
        if (GetComponent<UIBlockPlayerClick>() == null)
        {
            gameObject.AddComponent<UIBlockPlayerClick>();
        }
    }

    private void Start()
    {
        EnsureCheatButtons();
    }

    private void EnsureCheatButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        Button runBtn = null;

        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name;
            var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
            string bText = tmp != null ? tmp.text : string.Empty;

            if (bName.Contains("RunCurrency") || bText.Contains("Run Currency"))
            {
                runBtn = btn;
            }
            else if (bName.Contains("MetaCurrency") || bText.Contains("Meta Currency"))
            {
                // Disable any legacy or cloned MetaCurrency buttons
                btn.gameObject.SetActive(false);
            }
        }

        if (runBtn != null)
        {
            runBtn.onClick.RemoveListener(AddRunCurrency1000);
            runBtn.onClick.AddListener(AddRunCurrency1000);
        }
    }

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

    private Project.ShopSystem.PlayerRunInventory ResolveRunInventory()
    {
        if (runInventory != null) return runInventory;
        if (TeamLoader.Instance != null && TeamLoader.Instance.RunInventory != null)
        {
            runInventory = TeamLoader.Instance.RunInventory;
            return runInventory;
        }
        runInventory = Resources.Load<Project.ShopSystem.PlayerRunInventory>("InventorySO/PlayerRunInventory");
        return runInventory;
    }

    private Project.Progression.PlayerMetaProgression ResolveMetaProgression()
    {
        if (progressionInventory != null) return progressionInventory;
        if (TeamLoader.Instance != null && TeamLoader.Instance.MetaProgression != null)
        {
            progressionInventory = TeamLoader.Instance.MetaProgression;
            return progressionInventory;
        }
        progressionInventory = Resources.Load<Project.Progression.PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
        return progressionInventory;
    }

    public void AddRunCurrency(int amount = 1000)
    {
        var inv = ResolveRunInventory();
        if (inv != null)
        {
            inv.AddCurrency(amount);
            Debug.Log($"[AdminConsole] Added {amount} Run Currency. Current: {inv.currency}");

            // Notify ShopUI if active in scene
            var shopUI = Object.FindFirstObjectByType<Project.ShopSystem.UI.ShopUI>();
            shopUI?.UpdateCurrencyDisplay();
        }
        else
        {
            Debug.LogWarning("[AdminConsole] Cannot add Run Currency: PlayerRunInventory not found.");
        }
    }

    public void AddRunCurrency1000() => AddRunCurrency(1000);
    public void AddRunCurrency10000() => AddRunCurrency(10000);

    /// <summary>
    /// Unity OnClick prototype for the future Level-room reward sequence.
    /// </summary>
    public void TriggerGlobalLevelUpEvent()
    {
        var inv = ResolveRunInventory();
        if (inv == null)
        {
            Debug.LogWarning("[AdminConsole] Run Global Level event requires a PlayerRunInventory reference.");
            return;
        }

        int rolledLevels = RunGlobalLevelReward.Roll();
        int previousLevel = inv.GlobalLevel;
        int appliedLevels = inv.AddGlobalLevels(rolledLevels);

        Debug.Log(
            $"[AdminConsole] Run Global Level event rolled +{rolledLevels}, applied +{appliedLevels}, "
            + $"and changed Global Lv.{previousLevel} to Lv.{inv.GlobalLevel}.");
    }

    public void ResetPlayerRun()
    {
        var inv = ResolveRunInventory();
        if (inv != null)
        {
            inv.ResetRunProgress();
        }

        var meta = ResolveMetaProgression();
        if (meta != null)
        {
            meta.ClearAllProgression();
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
