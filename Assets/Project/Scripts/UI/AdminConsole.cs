
using UnityEngine;
using UnityEngine.UI;
using Project.Progression;

public class AdminConsole : MonoBehaviour
{

    [SerializeField] private GameObject enemyPrefab;

    [Header("Reset Systems")]
    [SerializeField] private Project.ShopSystem.PlayerRunInventory runInventory;
    [SerializeField] private Project.Progression.PlayerMetaProgression progressionInventory;

    private void Start()
    {
        EnsureCheatButtons();
    }

    private void EnsureCheatButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        Button runBtn = null;
        Button metaBtn = null;
        Button templateBtn = null;

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
                metaBtn = btn;
            }
            else if (templateBtn == null && btn.gameObject.activeInHierarchy)
            {
                templateBtn = btn;
            }
        }

        if (runBtn != null)
        {
            runBtn.onClick.RemoveListener(AddRunCurrency1000);
            runBtn.onClick.AddListener(AddRunCurrency1000);
        }

        if (metaBtn == null && runBtn != null)
        {
            // Clone Run button to create Meta Currency button in the same layout
            GameObject metaObj = Instantiate(runBtn.gameObject, runBtn.transform.parent);
            metaObj.name = "AddMetaCurrency";
            metaObj.SetActive(true);
            metaBtn = metaObj.GetComponent<Button>();
            metaBtn.onClick.RemoveAllListeners();
            metaBtn.onClick.AddListener(AddMetaCurrency1000);

            var metaText = metaObj.GetComponentInChildren<TMPro.TMP_Text>();
            if (metaText != null)
            {
                metaText.text = "Add Meta Currency (+1000)";
            }
        }
        else if (metaBtn != null)
        {
            metaBtn.onClick.RemoveListener(AddMetaCurrency1000);
            metaBtn.onClick.AddListener(AddMetaCurrency1000);
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

    public void AddMetaCurrency(int amount = 1000)
    {
        var meta = ResolveMetaProgression();
        if (meta != null)
        {
            meta.AddMetaCurrency(amount);
            Debug.Log($"[AdminConsole] Added {amount} Meta Currency. Current: {meta.MetaCurrency}");
        }
        else
        {
            Debug.LogWarning("[AdminConsole] Cannot add Meta Currency: PlayerMetaProgression not found.");
        }
    }

    public void AddBothCurrencies(int amount = 1000)
    {
        AddRunCurrency(amount);
        AddMetaCurrency(amount);
    }

    public void AddRunCurrency1000() => AddRunCurrency(1000);
    public void AddRunCurrency10000() => AddRunCurrency(10000);
    public void AddMetaCurrency1000() => AddMetaCurrency(1000);
    public void AddMetaCurrency10000() => AddMetaCurrency(10000);

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
