using System.Collections;
using Project.AIBehaviour.Boss;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Training
{
    public sealed class Boss112TestRoom : MonoBehaviour
    {
        [SerializeField] private Boss112Controller bossPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform playerStart;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private CinemachineCamera trainingCamera;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private TMP_Text debugLabel;
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private Button delayedButton;
        [SerializeField] private Button sweepingButton;
        [SerializeField] private LineRenderer[] debugLines;
        [SerializeField] private float arenaLeft = -18f, arenaRight = 18f, floorY;
        private readonly Vector3[] corners = new Vector3[4];
        private Boss112Controller boss;
        private Camera arenaCamera;
        private CinemachineBrain arenaBrain;
        private bool drawHitboxes;
        public Boss112Controller Boss => boss;

        private void Awake()
        {
            var runEnd = Object.FindFirstObjectByType<Project.Core.RunEnd.RunEndController>();
            if (runEnd != null && TrainingSession.IsTrainingContext)
            {
                runEnd.enabled = false;
                runEnd.StopAllCoroutines();
            }
            arenaCamera = Camera.main;
            if (arenaCamera == null) return;
            arenaBrain = arenaCamera.GetComponent<CinemachineBrain>();
            // This isolated arena needs a fixed frame, including while the boss leaves the screen.
            if (arenaBrain != null) arenaBrain.enabled = false;
            SetArenaCamera();
        }

        private void SetArenaCamera()
        {
            if (arenaCamera == null || cameraAnchor == null) return;
            arenaCamera.orthographic = true;
            arenaCamera.orthographicSize = 10.5f;
            arenaCamera.transform.SetPositionAndRotation(cameraAnchor.position + Vector3.back * 17f, Quaternion.identity);
        }

        private IEnumerator Start()
        {
            while (TrainingSession.Instance == null || !TrainingSession.Instance.IsActive || PlayerCharacter.Instance == null
                || PlayerCharacter.Instance.StateMachine == null) yield return null;
            yield return null;
            ResetPlayer();
            SpawnBoss();
        }

        public void SpawnBoss()
        {
            if (!TrainingSession.IsTrainingContext || bossPrefab == null || (boss != null && boss.Enemy.IsAlive)) return;
            RemoveBoss();
            boss = Instantiate(bossPrefab, spawnPoint.position, Quaternion.identity);
            boss.ConfigureArena(arenaLeft, arenaRight, floorY);
            boss.Enemy.InitializeSpawn(spawnPoint.position, Quaternion.identity);
        }

        public void RemoveBoss()
        {
            if (boss == null) return;
            boss.AutoAttack = false;
            boss.CancelPattern();
            boss.gameObject.SetActive(false);
            Destroy(boss.gameObject);
            boss = null;
        }

        public void TestPrecisionBeam() => StartPattern(Boss112Pattern.PrecisionBeam);
        public void TestHeavySlam() => StartPattern(Boss112Pattern.HeavySlam);
        public void TestTripleSlam() => StartPattern(Boss112Pattern.TripleSlam);
        public void TestDelayedSlam() => StartPattern(Boss112Pattern.DelayedSlam);
        public void TestSweepingBeam() => StartPattern(Boss112Pattern.SweepingBeam);
        public void ToggleTestPhase()
        {
            if (boss == null || !boss.Enemy.IsAlive) return;
            boss.AutoAttack = false;
            boss.CancelPattern();
            boss.Enemy.currentHP = boss.Enemy.maxHP * (boss.IsSecondPhase ? 1f : .5f);
        }
        private void StartPattern(Boss112Pattern pattern)
        {
            if (boss == null || !boss.Enemy.IsAlive) { SpawnBoss(); return; }
            boss.AutoAttack = false;
            boss.TryStartPattern(pattern);
        }

        public void ToggleAuto()
        {
            if (boss != null) boss.AutoAttack = !boss.AutoAttack;
        }

        public void StopAttack()
        {
            if (boss == null) return;
            boss.AutoAttack = false;
            boss.CancelPattern();
        }

        public void ToggleHitboxes() => drawHitboxes = !drawHitboxes;

        public void ResetTest()
        {
#if UNITY_EDITOR
            if (PlayerCharacter.Instance == null || PlayerCharacter.Instance.IsDead)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            RemoveBoss();
            ResetPlayer();
            SpawnBoss();
        }

        private void ResetPlayer()
        {
            var player = PlayerCharacter.Instance;
            var team = TeamLoader.Instance;
            if (team != null)
            {
                team.HealAndReviveEntireTeam();
                foreach (var character in team.GetTeamSnapshot())
                    if (character != null) team.SetCharacterCurrentAE(character, team.GetCharacterMaxAE(character));
            }
            if (player != null)
            {
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) { body.linearVelocity = Vector2.zero; body.position = playerStart.position; }
                player.transform.position = playerStart.position;
                player.StateMachine.ChangeState(player.StateMachine.IdleState);
            }
            TrainingSession.Instance?.ResetDamageSample();
        }

        private void LateUpdate()
        {
            SetArenaCamera();
            if (trainingCamera != null && cameraAnchor != null)
            {
                trainingCamera.Follow = cameraAnchor;
                trainingCamera.LookAt = null;
                trainingCamera.Priority = 250;
                var lens = trainingCamera.Lens;
                lens.OrthographicSize = 10.5f;
                trainingCamera.Lens = lens;
            }
            if (status != null) status.text = boss == null ? "BOSS 112 / NOT SPAWNED"
                : !boss.Enemy.IsAlive ? "BOSS 112 / DEFEATED" : !boss.Enemy.IsGameplayActive ? "BOSS 112 / SPAWNING"
                : "BOSS 112 / P" + (boss.IsSecondPhase ? "2 / " : "1 / ") + boss.Phase.ToString().ToUpperInvariant();
            if (autoLabel != null) autoLabel.text = boss != null && boss.AutoAttack ? "AUTO: ON" : "AUTO: OFF";
            if (debugLabel != null) debugLabel.text = drawHitboxes ? "HITBOX: ON" : "HITBOX: OFF";
            if (phaseLabel != null) phaseLabel.text = boss != null && boss.IsSecondPhase ? "PHASE: 2" : "PHASE: 1";
            bool advanced = boss != null && boss.Enemy.IsCombatReady && boss.IsSecondPhase && !boss.IsBusy;
            if (delayedButton != null) delayedButton.interactable = advanced;
            if (sweepingButton != null) sweepingButton.interactable = advanced;
            if (debugLines == null) return;
            for (int i = 0; i < debugLines.Length; i++)
            {
                var line = debugLines[i];
                bool visible = drawHitboxes && boss != null && boss.Enemy.IsAlive;
                line.enabled = visible;
                if (!visible) continue;
                Collider2D collider = i == 0 ? boss.Enemy.bodyCollider : i == 1 ? boss.Beam.DamageCollider : boss.ImpactCollider;
                if (i > 0 && !collider.enabled) { line.enabled = false; continue; }
                Bounds bounds = collider.bounds;
                corners[0] = new Vector3(bounds.min.x, bounds.min.y, 0f);
                corners[1] = new Vector3(bounds.min.x, bounds.max.y, 0f);
                corners[2] = new Vector3(bounds.max.x, bounds.max.y, 0f);
                corners[3] = new Vector3(bounds.max.x, bounds.min.y, 0f);
                line.SetPositions(corners);
            }
        }

        private void OnDestroy()
        {
            RemoveBoss();
            if (arenaBrain != null) arenaBrain.enabled = true;
        }
    }
}
