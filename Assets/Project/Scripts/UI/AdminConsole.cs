
using UnityEngine;
using UnityEngine.UI;
using Project.Progression;
using Project.UI;

[RequireComponent(typeof(UIBlockPlayerClick))]
public class AdminConsole : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private GameObject enemy101Prefab;
    [SerializeField] private GameObject enemy102Prefab;
    [SerializeField] private GameObject enemy103Prefab;

    [Header("Reset Systems")]
    [SerializeField] private Project.ShopSystem.CharacterInventoryState runInventory;
    [SerializeField] private Project.Progression.CharacterProgressionState progressionInventory;

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
        SpawnPrefab(PickRandomEnemyPrefab());
    }

    public void SpawnEnemy101()
    {
        SpawnPrefab(ResolvePrefab(enemy101Prefab != null ? enemy101Prefab : enemyPrefab, Enemy1AssetPath));
    }

    public void SpawnEnemy102()
    {
        SpawnPrefab(ResolvePrefab(enemy102Prefab, Enemy102AssetPath));
    }

    public void SpawnEnemy103()
    {
        SpawnPrefab(ResolvePrefab(enemy103Prefab, Enemy103AssetPath));
    }

    private const string Enemy1AssetPath = "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab";
    private const string Enemy102AssetPath = "Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab";
    private const string Enemy103AssetPath = "Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab";

    private static GameObject ResolvePrefab(GameObject assigned, string assetPath)
    {
        if (assigned != null)
            return assigned;

        // Variant conversion dropped the old root id, so a scene can keep a missing slot
        // even after the prefab asset is repaired. The editor can still load by path.
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        return null;
#endif
    }

    private void SpawnPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[AdminConsole] Cannot spawn enemy because no prefab is assigned.");
            return;
        }

        Instantiate(prefab, new Vector3(Random.Range(-10, 10), 0, 0), Quaternion.identity);
    }

    private GameObject PickRandomEnemyPrefab()
    {
        int validCount = 0;
        if (enemyPrefabs != null)
        {
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] != null)
                {
                    validCount++;
                }
            }
        }

        if (validCount > 0)
        {
            int pick = Random.Range(0, validCount);
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] == null)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return enemyPrefabs[i];
                }

                pick--;
            }
        }

        GameObject fallback = ResolvePrefab(enemyPrefab, Enemy1AssetPath);
        if (fallback != null)
            return fallback;
        fallback = ResolvePrefab(null, Enemy102AssetPath);
        if (fallback != null)
            return fallback;
        return ResolvePrefab(null, Enemy103AssetPath);
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

    private Project.ShopSystem.CharacterInventoryState ResolveRunInventory()
    {
        runInventory = Project.Training.CharacterStateContext.ResolveInventory(runInventory);
        if (runInventory != null) return runInventory;
        if (TeamLoader.Instance != null && TeamLoader.Instance.RunInventory != null)
        {
            runInventory = TeamLoader.Instance.RunInventory;
            return runInventory;
        }
        runInventory = Project.Training.CharacterStateContext.ResolveInventory();
        return runInventory;
    }

    private Project.Progression.CharacterProgressionState ResolveMetaProgression()
    {
        progressionInventory = Project.Training.CharacterStateContext.ResolveProgression(progressionInventory);
        if (progressionInventory != null) return progressionInventory;
        if (TeamLoader.Instance != null && TeamLoader.Instance.MetaProgression != null)
        {
            progressionInventory = TeamLoader.Instance.MetaProgression;
            return progressionInventory;
        }
        progressionInventory = Project.Training.CharacterStateContext.ResolveProgression();
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
