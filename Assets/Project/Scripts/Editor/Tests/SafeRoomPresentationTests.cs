#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class SafeRoomPresentationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void EventFocusNeverRetargetsOrReprioritizesRoomCameras()
    {
        var host = new GameObject("FocusTest");
        host.SetActive(false);
        var exploreObject = new GameObject("Explore");
        var staticObject = new GameObject("Static");
        var player = new GameObject("PlayerTarget");
        var display = new GameObject("DisplayTarget");
        CameraManager manager = null;
        try
        {
            manager = host.AddComponent<CameraManager>();
            var explore = exploreObject.AddComponent<CinemachineCamera>();
            var room = staticObject.AddComponent<CinemachineCamera>();
            explore.Follow = player.transform;
            explore.LookAt = player.transform;
            explore.Priority = 1;
            room.Priority = 20;
            Set(manager, "_exploreCam", explore);
            Set(manager, "_staticCam", room);

            manager.BeginTemporaryFocus(display.transform);
            var focus = (CinemachineCamera)Get(manager, "_focusCam");
            Assert.That(focus, Is.Not.SameAs(explore));
            Assert.That(focus.Follow, Is.SameAs(display.transform));
            Assert.That(explore.Follow, Is.SameAs(player.transform));
            Assert.That(explore.LookAt, Is.SameAs(player.transform));
            Assert.That(explore.Priority.Value, Is.EqualTo(1));
            Assert.That(room.Priority.Value, Is.EqualTo(20));
            Assert.That(room.Follow, Is.Null);

            manager.EndTemporaryFocus();
            Assert.That(Get(manager, "_focusCam"), Is.Null);
            Assert.That(explore.Follow, Is.SameAs(player.transform));
            Assert.That(room.Priority.Value, Is.EqualTo(20));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (manager != null)
                manager.EndTemporaryFocus();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(exploreObject);
            Object.DestroyImmediate(staticObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(display);
        }
    }

    [Test]
    public void TriggerExitOnInactiveZoneHidesPromptWithoutStartingCoroutine()
    {
        var zone = new GameObject("InteractionZone");
        zone.SetActive(false);
        var player = new GameObject("Player") { tag = "Player" };
        var promptObject = new GameObject("Prompt");
        try
        {
            var ui = zone.AddComponent<InteractableWorldUI>();
            var collider = player.AddComponent<BoxCollider2D>();
            Type promptType = typeof(InteractableWorldUI).GetNestedType("PromptRuntime", BindingFlags.NonPublic);
            object prompt = Activator.CreateInstance(promptType, true);
            promptType.GetField("Root").SetValue(prompt, promptObject);
            promptType.GetField("BaseScale").SetValue(prompt, Vector3.one);
            ((IList)Get(ui, "prompts")).Add(prompt);
            typeof(InteractableWorldUI).GetMethod("OnTriggerExit2D", PrivateInstance)
                .Invoke(ui, new object[] { collider });
            Assert.That(promptObject.activeSelf, Is.False);
            Assert.That(promptType.GetField("IsScaling").GetValue(prompt), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(zone);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(promptObject);
        }
    }

    private static object Get(object target, string field)
        => target.GetType().GetField(field, PrivateInstance).GetValue(target);

    [Test]
    public void PromptScaleTickCompletesOnceWithoutCoroutine()
    {
        var root = new GameObject("Prompt");
        try
        {
            Type promptType = typeof(InteractableWorldUI).GetNestedType("PromptRuntime", BindingFlags.NonPublic);
            object prompt = Activator.CreateInstance(promptType, true);
            promptType.GetField("Root").SetValue(prompt, root);
            promptType.GetField("IsScaling").SetValue(prompt, true);
            promptType.GetField("ScaleStart").SetValue(prompt, Vector3.one * 2f);
            promptType.GetField("ScaleEnd").SetValue(prompt, Vector3.zero);
            promptType.GetField("ScaleDuration").SetValue(prompt, 0.2f);
            int completed = 0;
            promptType.GetField("ScaleComplete").SetValue(prompt, (Action)(() => completed++));
            MethodInfo tick = typeof(InteractableWorldUI).GetMethod("TickScaleAnimation",
                BindingFlags.Static | BindingFlags.NonPublic);
            tick.Invoke(null, new[] { prompt, (object)0.1f });
            Assert.That(root.transform.localScale.x, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(completed, Is.Zero);
            tick.Invoke(null, new[] { prompt, (object)0.1f });
            tick.Invoke(null, new[] { prompt, (object)0.1f });
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(promptType.GetField("IsScaling").GetValue(prompt), Is.False);
            Assert.That(promptType.GetField("ScaleComplete").GetValue(prompt), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
}
#endif
