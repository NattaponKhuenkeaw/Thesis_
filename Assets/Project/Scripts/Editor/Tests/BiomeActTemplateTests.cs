#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class BiomeActTemplateTests
{
    [Test]
    public void DefaultTemplateHasFourCombatNodesBeforeBoss()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests));
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            Assert.That(director.CountCombatNodesBeforeBoss(), Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DefaultTemplateEndsWithBoss()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests) + "_Boss");
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            IReadOnlyList<BiomeActNode> template = director.BiomeTemplate;
            Assert.That(template[template.Count - 1], Is.EqualTo(BiomeActNode.Boss));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferCountIsAtLeastThree()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests) + "_Offers");
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            Assert.That(director.OfferCount, Is.GreaterThanOrEqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

#if UNITY_EDITOR
    [Test]
    public void ManagerPrefabOfferCountIsThree()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/Core/Manager.prefab");
        Assert.That(prefab, Is.Not.Null, "Manager prefab missing.");
        RoomChoiceDirector director = prefab.GetComponentInChildren<RoomChoiceDirector>(true);
        Assert.That(director, Is.Not.Null, "RoomChoiceDirector missing on Manager prefab.");
        Assert.That(director.OfferCount, Is.EqualTo(3));
    }
#endif
}
#endif
