using System.Collections.Generic;

public static class RoomEnemyRegistry
{
    private static readonly List<EnemyController> _enemies = new List<EnemyController>();

    public static void Register(EnemyController enemy)
    {
        if (enemy != null && !_enemies.Contains(enemy)) _enemies.Add(enemy);
    }

    public static void Unregister(EnemyController enemy)
    {
        if (enemy != null) _enemies.Remove(enemy);
    }

    public static void FillLiving(List<EnemyController> results)
    {
        results.Clear();
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = _enemies[i];
            if (enemy == null)
            {
                _enemies.RemoveAt(i);
                continue;
            }
            if (enemy.IsAlive) results.Add(enemy);
        }
    }

    public static void FillTargetable(List<EnemyController> results)
    {
        FillLiving(results);
        results.RemoveAll(enemy => !enemy.IsTargetable);
    }
}

