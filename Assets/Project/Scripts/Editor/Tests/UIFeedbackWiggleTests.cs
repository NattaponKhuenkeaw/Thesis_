#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class UIFeedbackWiggleTests
{
    private GameObject _host;
    private UIFeedbackDriver _driver;
    private RectTransform _target;

    [SetUp]
    public void SetUp()
    {
        _host = new GameObject("Wiggle tests");
        _driver = _host.AddComponent<UIFeedbackDriver>();
        _target = new GameObject("Dash slot", typeof(RectTransform)).GetComponent<RectTransform>();
        _target.SetParent(_host.transform);
        _target.anchoredPosition = new Vector2(40f, -12f);
    }

    [TearDown]
    public void TearDown()
    {
        DisableDriver();
        Object.DestroyImmediate(_host);
    }

    [Test]
    public void RepeatedRequestsKeepOneOriginalOriginAndDisableRestoresIt()
    {
        Vector2 origin = _target.anchoredPosition;
        for (int i = 0; i < 40; i++)
        {
            _driver.PlayWiggle(_target, _target.anchoredPosition);
            // Model a request arriving at the positive half of each wiggle.
            _target.anchoredPosition += Vector2.right * 4f;
        }
        var active = (IDictionary)typeof(UIFeedbackDriver).GetField("_activeWiggles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_driver);
        Assert.That(active.Count, Is.EqualTo(1));
        DisableDriver();
        Assert.That(_target.anchoredPosition, Is.EqualTo(origin));
        Assert.That(active.Count, Is.Zero);
    }

    [Test]
    public void IndependentTargetsAndLayoutMovementUseTheirOwnRestingPositions()
    {
        var other = new GameObject("Skill slot", typeof(RectTransform)).GetComponent<RectTransform>();
        other.SetParent(_host.transform); other.anchoredPosition = new Vector2(-100f, 10f);
        Vector2 origin = _target.anchoredPosition, otherOrigin = other.anchoredPosition;
        _driver.PlayWiggle(_target, origin); _driver.PlayWiggle(other, otherOrigin);
        _target.anchoredPosition += Vector2.right * 5f; other.anchoredPosition -= Vector2.right * 3f;
        DisableDriver();
        Assert.That(_target.anchoredPosition, Is.EqualTo(origin)); Assert.That(other.anchoredPosition, Is.EqualTo(otherOrigin));
        _driver.enabled = true; _target.anchoredPosition = new Vector2(70f, 20f);
        _driver.PlayWiggle(_target, _target.anchoredPosition);
        _target.anchoredPosition += Vector2.right * 3f;
        DisableDriver();
        Assert.That(_target.anchoredPosition, Is.EqualTo(new Vector2(70f, 20f)));
    }

    private void DisableDriver()
    {
        _driver.enabled = false;
        // EditMode does not dispatch MonoBehaviour disable callbacks automatically.
        typeof(UIFeedbackDriver).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_driver, null);
    }
}
#endif
