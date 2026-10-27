using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// One combat layout = one stage. Authored chambers play left-to-right;
    /// RouteOffer / stage exit unlock only after the last chamber is cleared.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CombatStageController : MonoBehaviour
    {
        [System.Serializable]
        public class Chamber
        {
            public string name = "Room";

            [Tooltip("Optional override. Leave empty so the chosen card (Easy / Medium / Hard) picks Encounter_Easy / Medium / Hard.")]
            public EncounterSO encounter;

            [Tooltip("How many enemies spawn in this room.")]
            [Min(0)]
            [UnityEngine.Serialization.FormerlySerializedAs("enemyQuota")]
            public int enemyCount = 3;

            [Tooltip("Solid wall on the left of this room.")]
            public Collider2D entranceWall;

            [Tooltip("Solid wall on the right. Shared with the next room's entrance when rooms are adjacent.")]
            public Collider2D exitWall;

            [Tooltip("Optional barriers for alternate paths. A barrier shared by the next chamber stays locked between rooms.")]
            public Collider2D[] additionalExitWalls;

            [Tooltip("Optional. Starts this room when the player walks in. Chamber 0 can rely on RoomWaveManager instead.")]
            public Collider2D enterTrigger;

            [Tooltip("Optional parent of Spawn_0..N for this room only.")]
            public Transform spawnRoot;

            [Tooltip("If empty, discovers Spawn_0..N under Spawn Root.")]
            public Transform[] spawnSlots;
        }

        [Header("Stage")]
        [Tooltip("Rooms in this combat layout, in play order. Count = how many rooms before the route choice.")]
        [SerializeField] private Chamber[] chambers = new Chamber[0];

        [Tooltip("Walk space into the next room after a clear, used when that room has no enter trigger.")]
        [SerializeField] [Min(0.5f)] private float corridorPadding = 6f;

        [Header("Scene Guides")]
        [Tooltip("Draw room bounds, labels, and spawn markers in the Scene view.")]
        [SerializeField] private bool drawSceneGuides = true;

        [Tooltip("Fallback height of the room overlay when walls are short or missing.")]
        [SerializeField] [Min(1f)] private float gizmoHeight = 8f;

        private RoomWaveManager waveManager;
        private SceneBoundaryController boundary;
        private List<GameObject>[] slices;
        private int currentIndex = -1;
        private int waitingForIndex = -1;
        private bool fighting;
        private Coroutine waitToEnterRoutine;

        public int ChamberCount => chambers != null ? chambers.Length : 0;

        public IReadOnlyList<Chamber> Chambers => chambers;

        public bool DrawSceneGuides => drawSceneGuides;

        public bool HasRemainingChambers =>
            chambers != null && currentIndex >= 0 && currentIndex < chambers.Length - 1;

        public bool TryGetChamberWalls(int index, out Collider2D entrance, out Collider2D exit)
        {
            entrance = null;
            exit = null;
            if (chambers == null || index < 0 || index >= chambers.Length || chambers[index] == null)
            {
                return false;
            }

            entrance = chambers[index].entranceWall;
            exit = chambers[index].exitWall;
            return entrance != null || exit != null;
        }

        private void Awake()
        {
            boundary = GetComponent<SceneBoundaryController>()
                ?? GetComponentInParent<SceneBoundaryController>()
                ?? (transform.root != null
                    ? transform.root.GetComponentInChildren<SceneBoundaryController>(true)
                    : GetComponentInChildren<SceneBoundaryController>(true));

            ArmEnterSensors();
        }

        private void Start()
        {
            BindPreviewChamberZero();
        }

        private void BindPreviewChamberZero()
        {
            if (boundary == null || chambers == null || chambers.Length == 0 || chambers[0] == null)
            {
                return;
            }

            boundary.BindChamberWalls(chambers[0].entranceWall, chambers[0].exitWall);
        }

        public bool HasSpawnContent(IReadOnlyList<GameObject> stageQuota)
        {
            if (chambers == null || chambers.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < chambers.Length; i++)
            {
                Chamber chamber = chambers[i];
                if (chamber == null || chamber.enemyCount <= 0)
                {
                    continue;
                }

                if (CountNonNull(ResolvePool(chamber, stageQuota)) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryBegin(RoomWaveManager waves, IReadOnlyList<GameObject> stageQuota)
        {
            waveManager = waves;
            if (waves == null || chambers == null || chambers.Length == 0)
            {
                return false;
            }

            slices = new List<GameObject>[chambers.Length];
            for (int i = 0; i < chambers.Length; i++)
            {
                Chamber chamber = chambers[i];
                int count = ResolveChamberCount(chamber);
                slices[i] = RollChamberQuota(chamber, stageQuota, count);
                if (count > 0 && slices[i].Count == 0)
                {
                    string roomName = chamber != null ? chamber.name : i.ToString();
                    Debug.LogError(
                        $"[CombatStageController] Chamber {i} ('{roomName}') has Enemy Count {count} but no enemy types. Need Encounter_Easy/Medium/Hard from the card, or an Encounter on this room.",
                        this);
                }
            }

            currentIndex = -1;
            waitingForIndex = -1;
            fighting = false;
            ArmEnterSensors();
            return BindAndStart(0);
        }

        private static int ResolveChamberCount(Chamber chamber)
        {
            int layoutCount = chamber != null ? chamber.enemyCount : 0;
            EncounterSO encounter = ResolveEncounter(chamber);
            if (encounter != null && encounter.ChamberQuota > 0)
            {
                return encounter.ChamberQuota;
            }

            return layoutCount;
        }

        private static List<GameObject> RollChamberQuota(
            Chamber chamber,
            IReadOnlyList<GameObject> stageQuota,
            int count)
        {
            EncounterSO encounter = ResolveEncounter(chamber);
            if (encounter != null && encounter.HasTypePool)
            {
                return encounter.RollQuota(count);
            }

            return CombatStageQuotaSplitter.Take(stageQuota, count);
        }

        private static IReadOnlyList<GameObject> ResolvePool(Chamber chamber, IReadOnlyList<GameObject> stageQuota)
        {
            EncounterSO encounter = ResolveEncounter(chamber);
            if (encounter != null)
            {
                List<GameObject> palette = encounter.CollectPalettePrefabs();
                if (CountNonNull(palette) > 0)
                {
                    return palette;
                }
            }

            return stageQuota;
        }

        private static EncounterSO ResolveEncounter(Chamber chamber)
        {
            return CombatRouteContent.ResolveChamberEncounter(chamber != null ? chamber.encounter : null);
        }

        private static int CountNonNull(IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        public void AdvanceAfterClear()
        {
            fighting = false;
            ReleaseAdditionalExitWalls();
            OpenBacktrackFromCurrent();
            if (!HasRemainingChambers)
            {
                return;
            }

            int nextIndex = currentIndex + 1;
            waitingForIndex = nextIndex;
            OpenCorridorToward(nextIndex);
        }

        private void ReleaseAdditionalExitWalls()
        {
            if (chambers == null || currentIndex < 0 || currentIndex >= chambers.Length) return;
            Chamber current = chambers[currentIndex];
            Chamber next = HasRemainingChambers ? chambers[currentIndex+1] : null;
            if (current?.additionalExitWalls == null) return;
            foreach (Collider2D wall in current.additionalExitWalls)
            {
                if (wall == null || (next != null && (next.exitWall == wall ||
                    (next.additionalExitWalls != null && System.Array.IndexOf(next.additionalExitWalls,wall) >= 0)))) continue;
                wall.enabled = false;
            }
        }

        private void OpenBacktrackFromCurrent()
        {
            if (boundary == null || chambers == null || currentIndex <= 0 || currentIndex >= chambers.Length)
            {
                return;
            }

            Chamber chamber = chambers[currentIndex];
            if (chamber != null)
            {
                boundary.OpenDivider(chamber.entranceWall);
            }
        }

        public void NotifyPlayerEntered(int chamberIndex, Collider2D other)
        {
            if (fighting || waitingForIndex < 0 || chamberIndex != waitingForIndex)
            {
                return;
            }

            if (!IsPlayer(other))
            {
                return;
            }

            BindAndStart(chamberIndex);
        }

        private bool BindAndStart(int index)
        {
            if (waveManager == null || chambers == null || index < 0 || index >= chambers.Length)
            {
                return false;
            }

            Chamber chamber = chambers[index];
            if (chamber == null)
            {
                Debug.LogError($"[CombatStageController] Chamber {index} is missing.", this);
                return false;
            }

            currentIndex = index;
            waitingForIndex = -1;
            fighting = true;
            if (chamber.additionalExitWalls != null)
                foreach (Collider2D wall in chamber.additionalExitWalls)
                    if (wall != null) { wall.enabled = true; wall.isTrigger = false; }
            if (waitToEnterRoutine != null)
            {
                StopCoroutine(waitToEnterRoutine);
                waitToEnterRoutine = null;
            }

            if (boundary != null)
            {
                if (index <= 0)
                {
                    if (!boundary.BindChamberWalls(chamber.entranceWall, chamber.exitWall))
                    {
                        Debug.LogError(
                            $"[CombatStageController] Chamber {index} ('{chamber.name}') needs entrance and exit walls.",
                            this);
                    }
                }
                else
                {
                    boundary.EnterLaterChamber(chamber.entranceWall, chamber.exitWall);
                }
            }

            Transform[] slots = ResolveSpawnSlots(chamber);
            if (boundary != null && chamber.exitWall != null && HasAssignedSlots(slots))
            {
                float farthest = float.NegativeInfinity;
                foreach (Transform slot in slots) if (slot != null) farthest = Mathf.Max(farthest,slot.position.x);
                if (farthest > chamber.exitWall.bounds.min.x)
                    boundary.OpenWalkTo(farthest+corridorPadding,null);
            }
            if (!HasAssignedSlots(slots))
            {
                Debug.LogError(
                    $"[CombatStageController] Chamber {index} ('{chamber.name}') has no spawn slots. Assign Spawn Slots or Spawn Root.",
                    this);
            }
            waveManager.BindSpawnSlots(slots);
            IReadOnlyList<GameObject> quota = slices != null && index < slices.Length
                ? slices[index]
                : null;
            waveManager.StartChamber(quota, ResolveEncounter(chamber));
            return true;
        }

        private void OpenCorridorToward(int nextIndex)
        {
            if (boundary == null || chambers == null || nextIndex < 0 || nextIndex >= chambers.Length)
            {
                return;
            }

            Chamber next = chambers[nextIndex];
            float right = corridorPadding;
            if (next != null && next.exitWall != null)
            {
                right = next.exitWall.bounds.min.x;
            }
            else if (next != null && next.enterTrigger != null)
            {
                right = next.enterTrigger.bounds.max.x;
            }
            else if (next != null && next.entranceWall != null)
            {
                right = next.entranceWall.bounds.max.x + corridorPadding;
            }
            else if (chambers[currentIndex] != null && chambers[currentIndex].exitWall != null)
            {
                right = chambers[currentIndex].exitWall.bounds.min.x + corridorPadding;
            }

            if (next != null && next.exitWall != null)
            {
                Transform[] slots=ResolveSpawnSlots(next);
                if (slots != null)
                    foreach (Transform slot in slots)
                        if (slot != null && slot.position.x > next.exitWall.bounds.min.x)
                            right=Mathf.Max(right,slot.position.x+corridorPadding);
            }

            Collider2D divider = chambers[currentIndex] != null ? chambers[currentIndex].exitWall : null;
            Collider2D back = currentIndex > 0 && chambers[currentIndex] != null
                ? chambers[currentIndex].entranceWall
                : null;
            boundary.OpenWalkTo(right, divider, back);

            if (next == null || next.enterTrigger == null)
            {
                if (waitToEnterRoutine != null)
                {
                    StopCoroutine(waitToEnterRoutine);
                }

                waitToEnterRoutine = StartCoroutine(WaitUntilPlayerPastDivider(nextIndex, divider));
            }
        }

        private IEnumerator WaitUntilPlayerPastDivider(int nextIndex, Collider2D divider)
        {
            const float pastGatePadding = 0.35f;
            while (waitingForIndex == nextIndex && !fighting)
            {
                PlayerCharacter player = PlayerCharacter.Instance;
                if (player != null)
                {
                    float gateX = divider != null
                        ? divider.bounds.max.x + pastGatePadding
                        : player.transform.position.x;
                    if (player.transform.position.x >= gateX)
                    {
                        waitToEnterRoutine = null;
                        BindAndStart(nextIndex);
                        yield break;
                    }
                }

                yield return null;
            }

            waitToEnterRoutine = null;
        }

        private void ArmEnterSensors()
        {
            if (chambers == null)
            {
                return;
            }

            for (int i = 1; i < chambers.Length; i++)
            {
                Chamber chamber = chambers[i];
                if (chamber != null && chamber.enterTrigger != null)
                {
                    ArmSensor(chamber.enterTrigger, i);
                }
            }
        }

        private void ArmSensor(Collider2D trigger, int chamberIndex)
        {
            if (trigger == null)
            {
                return;
            }

            trigger.isTrigger = true;
            CombatChamberEnterSensor sensor = trigger.GetComponent<CombatChamberEnterSensor>();
            if (sensor == null)
            {
                sensor = trigger.gameObject.AddComponent<CombatChamberEnterSensor>();
            }

            sensor.Configure(this, chamberIndex);
        }

        private static Transform[] ResolveSpawnSlots(Chamber chamber)
        {
            if (chamber == null)
            {
                return null;
            }

            if (HasAssignedSlots(chamber.spawnSlots))
            {
                return chamber.spawnSlots;
            }

            Transform root = chamber.spawnRoot;
            if (root == null)
            {
                return chamber.spawnSlots;
            }

            var discovered = new List<Transform>(4);
            for (int i = 0; i < 16; i++)
            {
                Transform slot = FindNamedChild(root, $"Spawn_{i}");
                if (slot == null)
                {
                    break;
                }

                discovered.Add(slot);
            }

            return discovered.Count > 0 ? discovered.ToArray() : chamber.spawnSlots;
        }

        private static bool HasAssignedSlots(Transform[] slots)
        {
            if (slots == null || slots.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindNamedChild(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i] != root && transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerCharacter>() != null)
            {
                return true;
            }

            Transform parent = other.transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag("Player"))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawSceneGuides || chambers == null)
            {
                return;
            }

            for (int i = 0; i < chambers.Length; i++)
            {
                DrawChamberGizmo(i, chambers[i]);
            }
        }

        private void DrawChamberGizmo(int index, Chamber chamber)
        {
            if (chamber == null)
            {
                return;
            }

            Color color = ChamberGuideColor(index);
            string encounterName = chamber.encounter != null ? chamber.encounter.name : "no Encounter";
            string roomLabel = string.IsNullOrWhiteSpace(chamber.name) || chamber.name == "Room"
                ? $"Room {index + 1} · {chamber.enemyCount} · {encounterName}"
                : $"Room {index + 1}: {chamber.name} · {chamber.enemyCount} · {encounterName}";

            bool hasBounds = RoomBoundaryMath.TryResolveLockedX(
                chamber.entranceWall,
                chamber.exitWall,
                null,
                out float left,
                out float right);

            float midY = ResolveGuideMidY(chamber);
            float height = ResolveGuideHeight(chamber);

            if (!hasBounds)
            {
                Vector3 missingAt = transform.position + Vector3.right * (index * 4f) + Vector3.up * 2f;
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.Label(missingAt, $"{roomLabel}\nmissing left/right wall");
                return;
            }

            Vector3 center = new Vector3((left + right) * 0.5f, midY, 0f);
            Vector3 size = new Vector3(Mathf.Max(0.2f, right - left), height, 0.05f);

            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.95f);
            Gizmos.DrawWireCube(center, size);

            Gizmos.DrawLine(new Vector3(left, midY - height * 0.5f, 0f), new Vector3(left, midY + height * 0.5f, 0f));
            Gizmos.DrawLine(new Vector3(right, midY - height * 0.5f, 0f), new Vector3(right, midY + height * 0.5f, 0f));

            var labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            labelStyle.normal.textColor = color;
            UnityEditor.Handles.Label(
                new Vector3(center.x, midY + height * 0.5f + 0.4f, 0f),
                roomLabel,
                labelStyle);

            var edgeStyle = new GUIStyle(UnityEditor.EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            edgeStyle.normal.textColor = color;
            UnityEditor.Handles.Label(new Vector3(left, midY, 0f), "Left", edgeStyle);
            UnityEditor.Handles.Label(new Vector3(right, midY, 0f), "Right", edgeStyle);

            if (chamber.enterTrigger != null)
            {
                Bounds triggerBounds = chamber.enterTrigger.bounds;
                Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.9f);
                Gizmos.DrawWireCube(triggerBounds.center, triggerBounds.size);
                UnityEditor.Handles.Label(
                    triggerBounds.center + Vector3.up * 0.4f,
                    $"Enter {roomLabel}",
                    edgeStyle);
            }

            Transform[] slots = ResolveSpawnSlots(chamber);
            if (slots == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s] == null)
                {
                    continue;
                }

                Vector3 spawnPos = slots[s].position;
                Gizmos.DrawSphere(spawnPos, 0.28f);
                UnityEditor.Handles.Label(spawnPos + Vector3.up * 0.45f, $"Spawn {s}", edgeStyle);
            }
        }

        private float ResolveGuideMidY(Chamber chamber)
        {
            if (chamber != null && chamber.entranceWall != null)
            {
                return chamber.entranceWall.bounds.center.y;
            }

            if (chamber != null && chamber.exitWall != null)
            {
                return chamber.exitWall.bounds.center.y;
            }

            return transform.position.y;
        }

        private float ResolveGuideHeight(Chamber chamber)
        {
            float height = gizmoHeight;
            if (chamber != null && chamber.entranceWall != null)
            {
                height = Mathf.Max(height, chamber.entranceWall.bounds.size.y);
            }

            if (chamber != null && chamber.exitWall != null)
            {
                height = Mathf.Max(height, chamber.exitWall.bounds.size.y);
            }

            return height;
        }

        private static Color ChamberGuideColor(int index)
        {
            switch (index % 4)
            {
                case 0: return new Color(0.2f, 0.85f, 1f);
                case 1: return new Color(1f, 0.85f, 0.2f);
                case 2: return new Color(1f, 0.5f, 0.2f);
                default: return new Color(0.95f, 0.4f, 1f);
            }
        }
#endif
    }
}
