using UnityEngine;
using UnityEngine.UI;

public class AdminConsole : MonoBehaviour
{

    [SerializeField] private GameObject enemyPrefab;


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

    public void HealPlayer()
    {
        
    }

    public void ResetLevel()
    {
        
    }

    public void ToggleTrainingDebug()
    {
        if (CombatCore.TrainingDebugManager.Instance != null)
        {
            CombatCore.TrainingDebugManager.Instance.ToggleDebugMode();
        }
    }
}
