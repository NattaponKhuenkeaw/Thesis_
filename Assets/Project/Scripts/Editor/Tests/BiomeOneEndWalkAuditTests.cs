#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BiomeOneEndWalkAuditTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private class Row
    {
        public int level, chamber;
        public string character;
        public bool cleared, fellBelowTerrain, reachedRoomEnd;
        public float step, startX, startY, gateX, finalX, finalY, lowestY, terrainBottom;
    }
    [Serializable] private class Report { public List<Row> rows = new List<Row>(); }

    [UnityTest]
    public IEnumerator RepeatedOrdinaryWalkingAtAllBiomeOneRoomEnds()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var report = new Report();
        var originalSimulation = Physics2D.simulationMode;
        float originalStep = Time.fixedDeltaTime;
        var tick = typeof(PlayerMovement).GetMethod("FixedUpdate", Private);
        Physics2D.simulationMode = SimulationMode2D.Script;
        try
        {
            for (int level = 1; level <= 5; level++)
            {
                string path = $"Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_{level:00}.prefab";
                byte[] saved = File.ReadAllBytes(path);
                var source = Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
                int count = source.GetComponentInChildren<CombatStageController>().ChamberCount;
                for (int chamberIndex = 0; chamberIndex < count; chamberIndex++)
                foreach (bool cleared in new[] { false, true })
                {
                    var root = Object.Instantiate(source);
                    var stage = root.GetComponentInChildren<CombatStageController>(); stage.enabled = false;
                    root.GetComponentInChildren<RoomWaveManager>().enabled = false;
                    var boundary = root.GetComponentInChildren<SceneBoundaryController>();
                    typeof(SceneBoundaryController).GetField("_boundExpandDuration", Private).SetValue(boundary, 0f);
                    try
                    {
                        var chamber = stage.Chambers[chamberIndex];
                        Physics2D.SyncTransforms();
                        boundary.BindChamberWalls(chamber.entranceWall, chamber.exitWall);
                        float gate = chamber.exitWall.bounds.min.x;
                        var slot = chamber.spawnSlots.Where(s => s != null && s.position.x < gate)
                            .OrderBy(s => Mathf.Abs(gate - s.position.x)).First();
                        var walls = stage.Chambers.SelectMany(c => new[] { c.entranceWall, c.exitWall }
                            .Concat(c.additionalExitWalls ?? new Collider2D[0])).Where(c => c != null).Distinct()
                            .Select(c => new { collider = c, position = c.transform.position, enabled = c.enabled, trigger = c.isTrigger }).ToArray();
                        Physics2D.SyncTransforms();
                        float bottom = root.transform.Find("Terrain").GetComponent<CompositeCollider2D>().bounds.min.y;
                        foreach (float step in new[] { originalStep, 1f / 30f, .05f })
                        foreach (string character in new[] { "test_0_Main/CharacterObject_main", "test_1_CameraGirl/CharacterObject_cg" })
                        {
                            foreach (var wall in walls)
                            {
                                wall.collider.transform.position = wall.position;
                                wall.collider.enabled = wall.enabled;
                                wall.collider.isTrigger = wall.trigger;
                            }
                            Physics2D.SyncTransforms();
                            boundary.BindChamberWalls(chamber.entranceWall, chamber.exitWall);
                            Time.fixedDeltaTime = step;
                            var actor = CreateMotor(character, slot.position + Vector3.up * .03f);
                            try
                            {
                                var row = new Row { level = level, chamber = chamberIndex + 1, cleared = cleared,
                                    character = character, step = step, gateX = gate, terrainBottom = bottom,
                                    startX = actor.RB.position.x, startY = actor.RB.position.y, lowestY = actor.RB.position.y };
                                int settleTicks = Mathf.CeilToInt(.3f / step);
                                for (int i = 0; i < settleTicks; i++) { tick.Invoke(actor, null); Physics2D.Simulate(step); }
                                actor.SetHorizontalInput(1f);
                                float speed = Mathf.Max(1f, actor.moveSpeedOverride);
                                int walkTicks = Mathf.CeilToInt((Mathf.Min(6f, Mathf.Abs(gate - row.startX) / speed) + 2f) / step);
                                int clearTick = Mathf.CeilToInt((Mathf.Abs(gate - row.startX) / speed + .2f) / step);
                                for (int i = 0; i < walkTicks; i++)
                                {
                                    if (cleared && i == clearTick)
                                    {
                                        typeof(CombatStageController).GetField("currentIndex", Private).SetValue(stage, chamberIndex);
                                        stage.AdvanceAfterClear();
                                        if (chamberIndex == count - 1) boundary.SetExitAvailable(true, false);
                                        Physics2D.SyncTransforms();
                                    }
                                    tick.Invoke(actor, null); Physics2D.Simulate(step);
                                    row.lowestY = Mathf.Min(row.lowestY, actor.RB.position.y);
                                    if (actor.RB.position.x >= gate - 1.2f) row.reachedRoomEnd = true;
                                    if (actor.RB.position.y < bottom - 2f) { row.fellBelowTerrain = true; break; }
                                }
                                row.finalX = actor.RB.position.x; row.finalY = actor.RB.position.y;
                                report.rows.Add(row);
                                if (row.fellBelowTerrain) Debug.LogWarning("[Ordinary walk fall] " + JsonUtility.ToJson(row));
                            }
                            finally { Object.DestroyImmediate(actor.gameObject); }
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                    yield return null;
                }
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved), "The audit must preserve saved map bytes");
                Debug.Log($"[Biome 1 walk audit] LV{level} completed. Falls so far: {report.rows.Count(r => r.fellBelowTerrain)}.");
            }
        }
        finally { Physics2D.simulationMode = originalSimulation; Time.fixedDeltaTime = originalStep; }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/biome-one-end-walk-audit.json", JsonUtility.ToJson(report, true));
        Assert.That(report.rows.Count, Is.GreaterThan(0));
        yield return new ExitPlayMode();
    }

    private static PlayerMovement CreateMotor(string character, Vector3 feetPosition)
    {
        var root = new GameObject("Ordinary walk physics probe"); root.SetActive(false);
        root.layer = LayerMask.NameToLayer("Player"); root.transform.position = feetPosition;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Player.prefab");
        var originalBody = source.GetComponent<Rigidbody2D>();
        var body = root.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = originalBody.collisionDetectionMode;
        int copiedColliders = 0;
        foreach (var originalCollider in source.GetComponentsInChildren<BoxCollider2D>(true)
            .Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<Rigidbody2D>(true) == originalBody))
        {
            var owner = root;
            if (originalCollider.transform != source.transform)
            {
                owner = new GameObject(originalCollider.name + " collision probe");
                owner.layer = originalCollider.gameObject.layer;
                owner.transform.SetParent(root.transform, false);
                owner.transform.localPosition = source.transform.InverseTransformPoint(originalCollider.transform.position);
                owner.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * originalCollider.transform.rotation;
                owner.transform.localScale = originalCollider.transform.lossyScale;
            }
            var collider = owner.AddComponent<BoxCollider2D>();
            collider.size = originalCollider.size; collider.offset = originalCollider.offset;
            collider.edgeRadius = originalCollider.edgeRadius;
            collider.excludeLayers = originalCollider.excludeLayers; collider.includeLayers = originalCollider.includeLayers;
            copiedColliders++;
        }
        Assert.That(copiedColliders, Is.GreaterThan(0), "The prefab asset must contribute physical colliders to the probe");
        var feet = new GameObject("Feet"); feet.transform.SetParent(root.transform, false);
        var motor = root.AddComponent<PlayerMovement>();
        var data = AssetDatabase.LoadAssetAtPath<CharacterObject>($"Assets/Project/Character/Hero/{character}.asset");
        motor.profile = data.movementProfile; motor.moveSpeedOverride = data.stats.speed;
        typeof(PlayerMovement).GetField("groundCheck", Private).SetValue(motor, feet.transform);
        root.SetActive(true);
        motor.enabled = false;
        Physics2D.SyncTransforms();
        return motor;
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
