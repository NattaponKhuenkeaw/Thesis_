#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public class RoomAmbienceTests
{
    private const string WindPath = "Assets/Project/Audio/Ambience/SafeRoom_Wind.wav";
    private GameObject host;
    private GameAudioManager manager;
    private AudioClip clip;
    private bool hadSfxPreference;
    private float sfxPreference;

    [SetUp]
    public void SetUp()
    {
        hadSfxPreference = PlayerPrefs.HasKey(GameSettings.SfxVolumeKey);
        sfxPreference = GameSettings.SfxVolume;
        host = new GameObject("RoomAmbienceTest");
        host.SetActive(false);
        manager = host.AddComponent<GameAudioManager>();
        clip = AudioClip.Create("RoomAmbienceTestClip", 44100, 1, 44100, false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(clip);
        if (hadSfxPreference)
            PlayerPrefs.SetFloat(GameSettings.SfxVolumeKey, sfxPreference);
        else
            PlayerPrefs.DeleteKey(GameSettings.SfxVolumeKey);
    }

    private AudioSource AmbienceSource => host.transform.Find("AmbienceAudioSource").GetComponent<AudioSource>();

    [TestCase("RoomData_Test_Shop")]
    [TestCase("RoomData_Rest_01")]
    [TestCase("RoomData_Level_01")]
    [TestCase("RoomData_Lobby")]
    [TestCase("RoomData_Combat_01")]
    [TestCase("RoomData_Combat_02")]
    [TestCase("RoomData_Combat_03")]
    [TestCase("RoomData_Combat_04")]
    [TestCase("RoomData_Combat_05")]
    [TestCase("RoomData_Boss_01")]
    [TestCase("RoomData_Test_Playground")]
    public void AllRoomsUseWindWithoutReplacingTheirMusic(string roomName)
    {
        var room = Resources.Load<RoomData>("MapSO/" + roomName);
        var wind = AssetDatabase.LoadAssetAtPath<AudioClip>(WindPath);
        Assert.That(room, Is.Not.Null);
        Assert.That(wind, Is.Not.Null);
        Assert.That(room.Ambience, Is.SameAs(wind));
        Assert.That(room.AmbienceVolume, Is.EqualTo(.35f));
        manager.PlayRoomMusic(room);
        Assert.That(manager.CurrentAmbience, Is.SameAs(wind));
        Assert.That(manager.CurrentMusic, Is.SameAs(room.BackgroundMusic));
        Assert.That(host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(2));
    }

    [Test]
    public void RoomWithoutAmbienceCanStillClearThePreviousAtmosphere()
    {
        var room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            manager.PlayAmbience(clip);
            manager.PlayRoomMusic(room);
            Assert.That(manager.CurrentAmbience, Is.Null);
            Assert.That(AmbienceSource.isPlaying, Is.False);
        }
        finally { Object.DestroyImmediate(room); }
    }

    [Test]
    public void SameAtmosphereKeepsItsPlaybackPositionAndDoesNotStackSources()
    {
        manager.PlayAmbience(clip);
        var source = AmbienceSource;
        source.timeSamples = 4000;
        manager.PlayAmbience(clip, .25f);
        Assert.That(source.timeSamples, Is.EqualTo(4000));
        Assert.That(AmbienceSource, Is.SameAs(source));
        Assert.That(host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
        Assert.That(source.loop, Is.True);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.ignoreListenerPause, Is.True);
    }

    [Test]
    public void SafeCombatAndBossRoomsReuseTheSameMusicAndAmbienceSources()
    {
        manager.PlayRoomMusic(Resources.Load<RoomData>("MapSO/RoomData_Test_Shop"));
        var source = AmbienceSource;
        manager.PlayRoomMusic(Resources.Load<RoomData>("MapSO/RoomData_Rest_01"));
        manager.PlayRoomMusic(Resources.Load<RoomData>("MapSO/RoomData_Level_01"));
        manager.PlayRoomMusic(Resources.Load<RoomData>("MapSO/RoomData_Combat_01"));
        manager.PlayRoomMusic(Resources.Load<RoomData>("MapSO/RoomData_Boss_01"));
        Assert.That(AmbienceSource, Is.SameAs(source));
        Assert.That(host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(2));
    }

    [Test]
    public void UnknownRoomClearsBothAudioLayers()
    {
        manager.PlayMusic(clip);
        manager.PlayAmbience(clip);
        manager.PlayRoomMusic(null);
        Assert.That(manager.CurrentMusic, Is.Null);
        Assert.That(manager.CurrentAmbience, Is.Null);
    }

    [Test]
    public void EmptyAmbienceDoesNotCreateAnUnusedSource()
    {
        manager.PlayAmbience(null);
        Assert.That(host.GetComponentsInChildren<AudioSource>(true), Is.Empty);
    }

    [Test]
    public void StoppingDoesNotBoostTheOutgoingWindToTheDefaultGain()
    {
        manager.PlayAmbience(clip, .35f);
        manager.PlayAmbience(null);
        float outgoingGain = (float)typeof(GameAudioManager)
            .GetField("ambienceBaseVolume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(manager);
        Assert.That(outgoingGain, Is.EqualTo(.35f));
    }

    [Test]
    public void SfxVolumeUpdatesWindLiveAndBgmVolumeDoesNotMuteIt()
    {
        var settings = new SerializedObject(manager);
        settings.FindProperty("masterVolume").floatValue = 1f;
        settings.FindProperty("sfxVolume").floatValue = .5f;
        settings.FindProperty("bgmVolume").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameSettings.SfxVolume = .4f;
        manager.PlayAmbience(clip, .35f);
        Assert.That(AmbienceSource.volume, Is.EqualTo(.07f).Within(.0001f));
        GameSettings.SfxVolume = 0f;
        manager.RefreshAmbienceVolume();
        Assert.That(AmbienceSource.volume, Is.Zero);
        GameSettings.SfxVolume = .8f;
        manager.RefreshAmbienceVolume();
        Assert.That(AmbienceSource.volume, Is.EqualTo(.14f).Within(.0001f));
    }

    [Test]
    public void WindStreamsWithoutDownmixingToMono()
    {
        var importer = AssetImporter.GetAtPath(WindPath) as AudioImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        Assert.That(importer.forceToMono, Is.False);
    }
}
#endif
