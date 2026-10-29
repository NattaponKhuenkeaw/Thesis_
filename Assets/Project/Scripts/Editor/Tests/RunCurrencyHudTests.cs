#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.ShopSystem;
using UnityEngine;

public class RunCurrencyHudTests
{
    [Test]
    public void FormatRunCurrencyWithoutDollarPrefix()
    {
        Assert.That(PlayerHUD.FormatRunCurrency(0), Is.EqualTo("0"));
        Assert.That(PlayerHUD.FormatRunCurrency(1000), Is.EqualTo("1000"));
        Assert.That(PlayerHUD.FormatRunCurrency(-25), Is.EqualTo("0"));
    }

    [Test]
    public void AddAndSpendCurrencyNotifyListeners()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            inventory.currency = 1000;
            int eventCount = 0;
            int lastValue = -1;
            inventory.OnCurrencyChanged += value =>
            {
                eventCount++;
                lastValue = value;
            };

            inventory.AddCurrency(250);
            Assert.That(inventory.currency, Is.EqualTo(1250));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(lastValue, Is.EqualTo(1250));

            inventory.SpendCurrency(50);
            Assert.That(inventory.currency, Is.EqualTo(1200));
            Assert.That(eventCount, Is.EqualTo(2));
            Assert.That(lastValue, Is.EqualTo(1200));

            inventory.AddCurrency(0);
            inventory.SpendCurrency(0);
            Assert.That(eventCount, Is.EqualTo(2));
            Assert.That(inventory.currency, Is.EqualTo(1200));

            inventory.SpendCurrency(9999);
            Assert.That(inventory.currency, Is.EqualTo(0));
            Assert.That(eventCount, Is.EqualTo(3));
            Assert.That(lastValue, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(inventory);
        }
    }
}
#endif
