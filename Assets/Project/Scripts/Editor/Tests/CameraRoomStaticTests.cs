using NUnit.Framework;
using Project.Core.ZoneState;
using Unity.Cinemachine;
using UnityEngine;
using System;
using System.Reflection;

public class CameraRoomStaticTests
{
    [Test]
    public void RoomData_UseStaticCamera_DefaultIsFalse()
    {
        RoomData room = ScriptableObject.CreateInstance<RoomData>();
        Assert.IsFalse(room.UseStaticCamera);
    }

    [Test]
    public void CameraManager_UpdateCameraForRoom_SwitchesPriorities()
    {
        GameObject cameraSetupObj = new GameObject("CameraSetUp");
        CameraManager cameraManager = cameraSetupObj.AddComponent<CameraManager>();

        GameObject exploreCamObj = new GameObject("CinemachineCamera_Explore");
        exploreCamObj.transform.SetParent(cameraSetupObj.transform);
        CinemachineCamera exploreCam = exploreCamObj.AddComponent<CinemachineCamera>();

        GameObject combatCamObj = new GameObject("CinemachineCamera_Combat");
        combatCamObj.transform.SetParent(cameraSetupObj.transform);
        CinemachineCamera combatCam = combatCamObj.AddComponent<CinemachineCamera>();

        GameObject staticCamObj = new GameObject("CinemachineCamera_Static");
        staticCamObj.transform.SetParent(cameraSetupObj.transform);
        CinemachineCamera staticCam = staticCamObj.AddComponent<CinemachineCamera>();

        // Set up dummy values for targets to check they get cleared
        GameObject dummyTarget = new GameObject("DummyTarget");
        staticCam.Follow = dummyTarget.transform;
        staticCam.LookAt = dummyTarget.transform;

        // Use reflection to set private fields on CameraManager
        Type type = typeof(CameraManager);
        type.GetField("_exploreCam", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, exploreCam);
        type.GetField("_combatCam", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, combatCam);
        type.GetField("_staticCam", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, staticCam);

        // Set active priorities
        type.GetField("_activePriority", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, 20);
        type.GetField("_inactivePriority", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, 0);

        // Create RoomData using static camera
        RoomData staticRoom = ScriptableObject.CreateInstance<RoomData>();
        FieldInfo useStaticField = typeof(RoomData).GetField("useStaticCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        useStaticField.SetValue(staticRoom, true);

        Assert.IsTrue(staticRoom.UseStaticCamera);

        // Invoke private UpdateCameraForRoom
        MethodInfo updateMethod = type.GetMethod("UpdateCameraForRoom", BindingFlags.NonPublic | BindingFlags.Instance);
        updateMethod.Invoke(cameraManager, new object[] { staticRoom });

        // Priorities should be swapped
        Assert.AreEqual(30, staticCam.Priority.Value); // _activePriority(20) + 10
        Assert.AreEqual(0, exploreCam.Priority.Value);  // _inactivePriority
        Assert.AreEqual(0, combatCam.Priority.Value);   // _inactivePriority
        
        // Targets should be cleared
        Assert.IsNull(staticCam.Follow);
        Assert.IsNull(staticCam.LookAt);

        // Switch to a non-static room
        RoomData normalRoom = ScriptableObject.CreateInstance<RoomData>();
        updateMethod.Invoke(cameraManager, new object[] { normalRoom });

        Assert.AreEqual(0, staticCam.Priority.Value);
        Assert.AreEqual(20, exploreCam.Priority.Value); // SetExploreMode should activate exploreCam

        // Clean up
#if UNITY_EDITOR
        if (UnityEditor.Selection.activeGameObject != null && 
            (UnityEditor.Selection.activeGameObject == cameraSetupObj || UnityEditor.Selection.activeGameObject.transform.IsChildOf(cameraSetupObj.transform)))
        {
            UnityEditor.Selection.activeGameObject = null;
        }
#endif
        UnityEngine.Object.DestroyImmediate(cameraSetupObj);
        UnityEngine.Object.DestroyImmediate(dummyTarget);
    }

    [Test]
    public void CameraManager_SetUltimateZoom_CachesAndChangesSizes()
    {
        GameObject cameraSetupObj = new GameObject("CameraSetUp");
        CameraManager cameraManager = cameraSetupObj.AddComponent<CameraManager>();

        GameObject exploreCamObj = new GameObject("CinemachineCamera_Explore");
        exploreCamObj.transform.SetParent(cameraSetupObj.transform);
        CinemachineCamera exploreCam = exploreCamObj.AddComponent<CinemachineCamera>();

        // Set initial sizes
        var lens = exploreCam.Lens;
        lens.OrthographicSize = 7f;
        exploreCam.Lens = lens;

        // Use reflection to set private fields on CameraManager
        Type type = typeof(CameraManager);
        type.GetField("_exploreCam", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cameraManager, exploreCam);

        // Check initialization value
        Assert.AreEqual(7f, exploreCam.Lens.OrthographicSize);

        // Enable zoom
        cameraManager.SetUltimateZoom(true, 4f);
        Assert.AreEqual(4f, exploreCam.Lens.OrthographicSize);

        // Disable zoom - should restore to cached size (7f)
        cameraManager.SetUltimateZoom(false);
        Assert.AreEqual(7f, exploreCam.Lens.OrthographicSize);

        // Clean up
        UnityEngine.Object.DestroyImmediate(cameraSetupObj);
    }
}
