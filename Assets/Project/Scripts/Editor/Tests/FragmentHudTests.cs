#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.Progression;
using UnityEngine;

public class FragmentHudTests
{
    [Test]
    public void FormatCharacterFragmentsUsesFragPrefix()
    {
        Assert.That(PlayerHUD.FormatCharacterFragments(0), Is.EqualTo("Frag 0"));
        Assert.That(PlayerHUD.FormatCharacterFragments(87), Is.EqualTo("Frag 87"));
        Assert.That(PlayerHUD.FormatCharacterFragments(-3), Is.EqualTo("Frag 0"));
    }

    [Test]
    public void AddAndSpendFragmentsNotifyListeners()
    {
        PlayerMetaProgression progression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterItem item = ScriptableObject.CreateInstance<CharacterItem>();
        try
        {
            item.id = "char-test-fragment-hud";
            int eventCount = 0;
            progression.OnFragmentsChanged += () => eventCount++;

            progression.AddFragments(item, 3);
            Assert.That(progression.GetFragmentCount("char-test-fragment-hud"), Is.EqualTo(3));
            Assert.That(eventCount, Is.EqualTo(1));

            Assert.That(progression.TrySpendFragments("char-test-fragment-hud", ProgressionFragmentType.CharacterPotential, 1), Is.True);
            Assert.That(progression.GetFragmentCount("char-test-fragment-hud"), Is.EqualTo(2));
            Assert.That(eventCount, Is.EqualTo(2));

            progression.AddFragments(item, 0);
            Assert.That(eventCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(progression);
        }
    }
}
#endif
