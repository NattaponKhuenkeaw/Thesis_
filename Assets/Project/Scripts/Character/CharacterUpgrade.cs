using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class CharacterUpgrade
{
    // =========================================================
    // Upgrade Info
    // =========================================================
    [Header("Upgrade Info")]
    public int level = 1;
    public int constellationRank = 0;

    [FormerlySerializedAs("skillTreePoints")]
    public int progressionPoints = 3;
}
