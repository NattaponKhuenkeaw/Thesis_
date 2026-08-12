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
}
#endif
