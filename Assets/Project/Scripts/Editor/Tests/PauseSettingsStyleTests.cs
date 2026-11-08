#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class PauseSettingsStyleTests
{
    private const string SettingsPath = "Assets/Project/Prefab/UI/SettingsPanel.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

    [Test]
    public void SettingsIsOneAuthoredPrefabUsedByBothMenuAndPause()
    {
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
        Assert.That(shared, Is.Not.Null);
        var pause = Resources.Load<PauseMenuController>("UI/PauseMenu");
        AssertSharedSettings(pause, shared);
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity", OpenSceneMode.Additive);
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
            AssertSharedSettings(menu, shared);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }

        var save = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/SaveSelectPanel.prefab");
        Assert.That(shared.transform.Find("Panel").GetComponent<Image>().sprite,
            Is.EqualTo(save.transform.Find("Overlay/Window").GetComponent<Image>().sprite));
        Assert.That(shared.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(3));
        Assert.That(shared.GetComponentsInChildren<Toggle>(true).Length, Is.EqualTo(1));
        Assert.That(shared.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(1));
        Assert.That(shared.GetComponentsInChildren<Selectable>(true).All(s => s.navigation.mode == Navigation.Mode.Explicit), Is.True);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        foreach (var text in shared.GetComponentsInChildren<TMP_Text>(true).Where(t => t.gameObject.activeSelf))
            Assert.That(text.font, Is.EqualTo(font), text.name);
    }

    private static void AssertSharedSettings(object controller, GameObject shared)
    {
        var settings = Get<GameObject>(controller, "settingsPanel");
        Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(settings), Is.EqualTo(shared));
        foreach (string field in new[] { "masterSlider", "bgmSlider", "sfxSlider", "fullscreenToggle", "settingsBackButton" })
        {
            var control = Get<Component>(controller, field);
            Assert.That(control, Is.Not.Null, field);
            Assert.That(control.transform.IsChildOf(settings.transform), Is.True, field);
        }
        Assert.That(Get<Button>(controller, "settingsBackButton").GetComponent<Image>().sprite.name, Is.EqualTo("UI_X"));
        Assert.That(settings.GetComponent<UIVisibilityTransition>(), Is.Not.Null);
    }

    [Test]
    public void HudSettingButtonOpensThePauseMenu()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/CombatUI/Player_UI.prefab");
        var button = hud.GetComponentsInChildren<Button>(true).Single(b => b.name == "Setting");
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(hud.GetComponentInChildren<PlayerHUD>(true)));
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(PlayerHUD.OpenPauseMenu)));
    }

    [Test]
    public void PauseUsesSaveArtworkAndAStoredResumeCloseBinding()
    {
        var pause = Resources.Load<PauseMenuController>("UI/PauseMenu");
        var save = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/SaveSelectPanel.prefab");
        var main = Get<GameObject>(pause, "mainPanel");
        Assert.That(main.GetComponent<Image>().sprite, Is.EqualTo(save.transform.Find("Overlay/Window").GetComponent<Image>().sprite));
        foreach (string field in new[] { "resumeButton", "settingsButton", "returnToTitleButton", "confirmCancelButton", "confirmOkButton" })
        {
            var button = Get<Button>(pause, field);
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(button.GetComponent<Image>().sprite.name, Is.EqualTo("UI_BlackBox_GreyBorder_NoOutLine"));
            Assert.That(button.spriteState.selectedSprite.name, Is.EqualTo("UI_BlackBox_YellowBorder_NoOutLine"));
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        }
        var close = main.transform.Find("ClosePauseButton").GetComponent<Button>();
        Assert.That(close.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(close.onClick.GetPersistentTarget(0), Is.EqualTo(pause));
        Assert.That(close.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(PauseMenuController.OnResumeClicked)));
    }

    [UnityTest]
    public IEnumerator SettingsControlsAndEscapeWorkWithoutCreatingUiObjects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var pause = PauseMenuController.Instance;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);
        string[] keys = { GameSettings.MasterVolumeKey, GameSettings.BgmVolumeKey, GameSettings.SfxVolumeKey };
        bool[] hadKeys = keys.Select(PlayerPrefs.HasKey).ToArray();
        float[] previousValues = keys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
        float previousListenerVolume = AudioListener.volume;
        try
        {
            pause.OpenPauseMenu();
            var main = Get<GameObject>(pause, "mainPanel");
            var settings = Get<GameObject>(pause, "settingsPanel");
            Assert.That(PauseMenuController.IsOpen, Is.True);
            Assert.That(main.activeInHierarchy, Is.True);
            Assert.That(settings.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Get<Button>(pause, "resumeButton").gameObject));

            Get<Button>(pause, "settingsButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(settings.activeInHierarchy, Is.True);
            int authoredCount = pause.GetComponentsInChildren<Transform>(true).Count(t => t.GetComponent<TMP_SubMeshUI>() == null);
            Get<Slider>(pause, "masterSlider").value = .41f;
            Get<Slider>(pause, "bgmSlider").value = .52f;
            Get<Slider>(pause, "sfxSlider").value = .63f;
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(.41f).Within(.001f));
            Assert.That(GameSettings.BgmVolume, Is.EqualTo(.52f).Within(.001f));
            Assert.That(GameSettings.SfxVolume, Is.EqualTo(.63f).Within(.001f));
            Assert.That(Time.timeScale, Is.Zero);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            InputSystem.Update();
            Call(pause, "Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(settings.activeSelf, Is.False, "Escape closes Settings first");
            Assert.That(PauseMenuController.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Get<Button>(pause, "resumeButton").gameObject));

            pause.OnSettingsClicked();
            Get<Button>(pause, "settingsBackButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(settings.activeSelf, Is.False);
            Assert.That(pause.GetComponentsInChildren<Transform>(true).Count(t => t.GetComponent<TMP_SubMeshUI>() == null), Is.EqualTo(authoredCount));
            var returnButton = Get<Button>(pause, "returnToTitleButton");
            var cancelButton = Get<Button>(pause, "confirmCancelButton");
            returnButton.onClick.Invoke();
            Assert.That(Get<GameObject>(pause, "confirmPanel").activeInHierarchy, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cancelButton.gameObject));
            cancelButton.onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(returnButton.gameObject));

            main.transform.Find("ClosePauseButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(PauseMenuController.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(InputLockManager.IsLocked, Is.False);
            Assert.That(AudioListener.pause, Is.False);
        }
        finally
        {
            if (PauseMenuController.IsOpen) pause.OnResumeClicked();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            for (int i = 0; i < keys.Length; i++)
                if (hadKeys[i]) PlayerPrefs.SetFloat(keys[i], previousValues[i]); else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
            AudioListener.volume = previousListenerVolume;
            Time.timeScale = 1;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
