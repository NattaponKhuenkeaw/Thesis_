using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Boss112VisualMotion : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float bobHeight = .16f;
        [SerializeField, Min(.01f)] private float bobFrequency = .65f;
        [SerializeField, Min(0f)] private float swayDegrees = 2f;
        [SerializeField, Min(0f)] private float movementLeanDegrees = 8f;
        [SerializeField, Min(.1f)] private float leanResponse = 7f;
        [SerializeField, Min(0f)] private float chargeShake = .045f;
        [SerializeField, Min(1f)] private float chargeFrequency = 22f;
        [Header("Shot Recoil")]
        [SerializeField, Min(0f)] private float shotRecoilHeight = .32f;
        [SerializeField, Min(.1f)] private float shotRecoilDuration = .32f;
        [Header("Stunned Pose")]
        [SerializeField, Min(0f)] private float stunSwayDegrees = 3f;
        [SerializeField, Min(.1f)] private float stunSwayFrequency = .85f;

        private Boss112Controller boss;
        private Vector3 restPosition, previousRootPosition, pivot;
        private Quaternion restRotation;
        private float clock, lean, stunAge;

        private void Awake()
        {
            boss = GetComponentInParent<Boss112Controller>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            var sprite = GetComponent<SpriteRenderer>().sprite;
            pivot = sprite != null ? sprite.bounds.center : Vector3.zero;
            if (boss != null) previousRootPosition = boss.transform.position;
        }

        private void LateUpdate()
        {
            if (boss == null) return;
            Vector3 rootPosition = boss.transform.position;
            float dt = Time.deltaTime;
            float velocityX = dt > 0f ? (rootPosition.x - previousRootPosition.x) / dt : 0f;
            previousRootPosition = rootPosition;
            bool slam = boss.Phase == Boss112Phase.SlamTracking || boss.Phase == Boss112Phase.SlamLocked
                || boss.Phase == Boss112Phase.SlamFalling || boss.Phase == Boss112Phase.SlamImpact
                || boss.Phase == Boss112Phase.SlamFeint || boss.Phase == Boss112Phase.SlamCommit
                || boss.Phase == Boss112Phase.Recovery;
            if (!boss.Enemy.IsCombatReady || !boss.Enemy.IsAlive || boss.Enemy.IsGrabbed)
            {
                stunAge = 0f;
                RestorePose();
                return;
            }
            if (dt <= 0f) return;
            if (slam)
            {
                RestorePose();
                if (boss.IsStunPoseHeld)
                {
                    stunAge += dt;
                    float sway = Mathf.Sin(stunAge * Mathf.PI * 2f * stunSwayFrequency) * stunSwayDegrees
                        * Mathf.SmoothStep(0f, 1f, stunAge / .2f);
                    transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, sway);
                }
                else stunAge = 0f;
                return;
            }
            stunAge = 0f;
            clock += dt;
            float charge = boss.BeamChargeProgress;
            bool locked = boss.Phase == Boss112Phase.BeamLocked || boss.Phase == Boss112Phase.BeamActive
                || boss.Phase == Boss112Phase.SweepActive;
            float desiredLean = locked ? 0f : -Mathf.Clamp(velocityX / 12f, -1f, 1f) * movementLeanDegrees;
            lean = Mathf.Lerp(lean, desiredLean, 1f - Mathf.Exp(-leanResponse * dt));
            float wave = Mathf.Sin(clock * Mathf.PI * 2f * bobFrequency);
            float bob = wave * bobHeight * (1f - charge * .7f);
            if (rootPosition.y < boss.FloorY + .5f) bob = (wave + 1f) * .5f * bobHeight;
            float shake = chargeShake * charge * charge;
            float noise = clock * Mathf.PI * 2f * chargeFrequency;
            var offset = new Vector3(Mathf.Sin(noise) * shake,
                bob + Mathf.Sin(noise * 1.37f) * shake * .6f, 0f);
            if (boss.Phase == Boss112Phase.BeamActive || boss.Phase == Boss112Phase.SweepActive)
                offset.y += shotRecoilHeight * Pulse(boss.PhaseElapsed, shotRecoilDuration);
            float roll = lean + Mathf.Sin(clock * Mathf.PI * bobFrequency) * swayDegrees * (1f - charge)
                + Mathf.Sin(noise * .83f) * charge * .4f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, roll);
            Vector3 scaledPivot = Vector3.Scale(pivot, transform.localScale);
            // Rotate around the head center, not the bottom-of-frame sprite pivot.
            transform.localPosition = restPosition + offset + restRotation * (scaledPivot - rotation * scaledPivot);
            transform.localRotation = restRotation * rotation;
        }

        private static float Pulse(float age, float duration)
        {
            if (age <= 0f || age >= duration) return 0f;
            float t = age / duration;
            return t < .25f ? Mathf.SmoothStep(0f, 1f, t * 4f)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - .25f) / .75f);
        }

        private void RestorePose()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            lean = 0f;
        }

        private void OnDisable() => RestorePose();
    }
}
