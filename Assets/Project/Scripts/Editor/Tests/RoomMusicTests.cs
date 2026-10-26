#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public class RoomMusicTests
{
    private const string MusicPath = "Assets/Project/Audio/Music/ES_Was It Just a Dream - Hanna Lindgren.mp3";
    private const string CombatMusicPath = "Assets/Project/Audio/Music/ES_Gone Without A Trace 1 - Fredrik Ekstrom.mp3";
    private GameObject host;
    private GameAudioManager manager;
    private AudioClip clip;
    private bool hadBgmPreference;
    private float bgmPreference;
    private float listenerVolume;

    [SetUp]
    public void SetUp()
    {
        hadBgmPreference = PlayerPrefs.HasKey(GameSettings.BgmVolumeKey);
        bgmPreference = GameSettings.BgmVolume;
        listenerVolume = AudioListener.volume;
        host = new GameObject("RoomMusicTest");
        host.SetActive(false);
        manager = host.AddComponent<GameAudioManager>();
        clip = AudioClip.Create("RoomMusicTestClip", 44100, 1, 44100, false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(clip);
        if (hadBgmPreference)
            PlayerPrefs.SetFloat(GameSettings.BgmVolumeKey, bgmPreference);
        else
            PlayerPrefs.DeleteKey(GameSettings.BgmVolumeKey);
        AudioListener.volume = listenerVolume;
    }

    [TestCase("RoomData_Lobby")]
    [TestCase("RoomData_Test_Shop")]
    [TestCase("RoomData_Rest_01")]
    [TestCase("RoomData_Level_01")]
    public void SafeRoomsReferenceChosenSong(string roomName)
    {
        var room = Resources.Load<RoomData>("MapSO/" + roomName);
        var song = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        Assert.That(song, Is.Not.Null);
        Assert.That(room.BackgroundMusic, Is.SameAs(song));
        manager.PlayRoomMusic(room);
        Assert.That(manager.CurrentMusic, Is.SameAs(song));
    }

    [Test]
    public void SameSongKeepsPlaybackPositionAndUsesOneNonSpatialSource()
    {
        manager.PlayMusic(clip);
        var source = host.GetComponentInChildren<AudioSource>(true);
        source.timeSamples = 4000;
        manager.PlayMusic(clip, .5f);
        Assert.That(source.timeSamples, Is.EqualTo(4000));
        Assert.That(host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
        Assert.That(source.loop, Is.True);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.ignoreListenerPause, Is.True);
    }

    [TestCase("RoomData_Combat_01")]
    [TestCase("RoomData_Combat_02")]
    [TestCase("RoomData_Combat_03")]
    [TestCase("RoomData_Combat_04")]
    [TestCase("RoomData_Combat_05")]
    [TestCase("RoomData_Boss_01")]
    public void CombatRoomsSwitchToChosenBattleSong(string roomName)
    {
        var safeRoom = Resources.Load<RoomData>("MapSO/RoomData_Rest_01");
        var combatRoom = Resources.Load<RoomData>("MapSO/" + roomName);
        var battleSong = AssetDatabase.LoadAssetAtPath<AudioClip>(CombatMusicPath);
        Assert.That(battleSong, Is.Not.Null);
        Assert.That(combatRoom.BackgroundMusic, Is.SameAs(battleSong));
        manager.PlayRoomMusic(safeRoom);
        var source = host.GetComponentInChildren<AudioSource>(true);
        manager.PlayRoomMusic(combatRoom);
        Assert.That(manager.CurrentMusic, Is.SameAs(battleSong));
        manager.PlayRoomMusic(safeRoom);
        Assert.That(manager.CurrentMusic, Is.SameAs(safeRoom.BackgroundMusic));
        Assert.That(host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(2), "Music and ambience have independent sources.");
        Assert.That(host.GetComponentInChildren<AudioSource>(true), Is.SameAs(source));
    }

    [Test]
    public void RoomWithoutMusicClearsPreviousTrack()
    {
        var room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            manager.PlayMusic(clip);
            manager.PlayRoomMusic(room);
            Assert.That(manager.CurrentMusic, Is.Null);
        }
        finally { Object.DestroyImmediate(room); }
    }

    [Test]
    public void UnknownRoomClearsMusic()
    {
        manager.PlayMusic(clip);
        manager.PlayRoomMusic(null);
        Assert.That(manager.CurrentMusic, Is.Null);
    }

    [Test]
    public void BgmSettingUpdatesSourceWithoutApplyingListenerMasterAgain()
    {
        var settings = new SerializedObject(manager);
        settings.FindProperty("bgmVolume").floatValue = .6f;
        settings.FindProperty("masterVolume").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameSettings.BgmVolume = .4f;
        AudioListener.volume = .25f;
        manager.PlayMusic(clip, .5f);
        var source = host.GetComponentInChildren<AudioSource>(true);
        Assert.That(source.volume, Is.EqualTo(.12f).Within(.0001f));
        GameSettings.BgmVolume = 0f;
        manager.RefreshMusicVolume();
        Assert.That(source.volume, Is.Zero);
        GameSettings.BgmVolume = .8f;
        manager.RefreshMusicVolume();
        Assert.That(source.volume, Is.EqualTo(.24f).Within(.0001f));
    }

    [TestCase(MusicPath)]
    [TestCase(CombatMusicPath)]
    public void LongMusicStreamsWithoutDownmixingToMono(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        Assert.That(importer.forceToMono, Is.False);
    }
}
#endif
