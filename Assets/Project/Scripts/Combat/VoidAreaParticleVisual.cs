using UnityEngine;

namespace Project.Combat
{
    [DisallowMultipleComponent]
    public sealed class VoidAreaParticleVisual : MonoBehaviour
    {
        [Header("Ground Field")]
        [Tooltip("Presentation height in world units, independent of the damage hitbox height.")]
        [Min(0.1f)] public float height = 2f;
        public ParticleSystem groundGlow;
        public ParticleSystem flames;
        public ParticleSystem smoke;

        [Header("Void Accents")]
        [Tooltip("Soft shadow halo behind the ground flames.")]
        public ParticleSystem blackHalo;
        [Tooltip("Sparse flame fragments that rise, curl and dissolve.")]
        public ParticleSystem floatingFlames;

        public float FieldWidth { get; private set; }
        private MaterialPropertyBlock _properties;

        public void Begin(ProjectileData data, ProjectileSpawnContext context)
        {
            Clear();
            if (data == null) return;

            float multiplier = context.sizeMultiplier > 0f ? context.sizeMultiplier : 1f;
            if (!ProjectileSpatialUtility.TryGetHitboxGeometry(data.ResolveRoutineHitbox(),
                    context.direction.x, out Vector2 offset, out Vector2 size))
            {
                offset = data.ResolvePassiveColliderOffset(context.direction.x);
                size = data.travelColliderSize;
            }

            FieldWidth = Mathf.Max(0.1f, Mathf.Abs(size.x) * multiplier);
            // The projectile already owns facing rotation. Convert the world-facing X offset once.
            Vector3 center = new Vector3(offset.x, 0f, 0f);
            center = transform.InverseTransformVector(center);
            Configure(blackHalo, center, 0.28f, 2.6f, 0.9f, 0.025f, 2f, 0.45f);
            Configure(groundGlow, center, 0.055f, 1.3f, 0.16f, 0f, 0.85f, 1.8f);
            Configure(flames, center, 0.26f, 1.65f, 0.85f, 0.15f, 1.6f, 1.15f);
            Configure(smoke, center, 0.48f, 2.1f, 0.8f, 0.12f, 2f, 0.65f);
            Configure(floatingFlames, center, 0.16f, 0.065f, 0.055f, 0.36f, 2.4f, 0.45f);
        }

        public void Clear()
        {
            Stop(groundGlow);
            Stop(flames);
            Stop(smoke);
            Stop(blackHalo);
            Stop(floatingFlames);
        }

        private void Configure(ParticleSystem particles, Vector3 center, float sourceY,
            float width, float sizeY, float rise, float lifetime, float density)
        {
            if (particles == null) return;
            float particleWidth = Mathf.Min(width, FieldWidth);
            particles.transform.localPosition = center + Vector3.up * (sourceY * height);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(particleWidth * 0.6f, particleWidth);
            main.startSizeY = new ParticleSystem.MinMaxCurve(sizeY * height * 0.65f, sizeY * height);
            main.startSizeZ = 1f;
            var shape = particles.shape;
            shape.scale = new Vector3(Mathf.Max(0f, FieldWidth - particleWidth), 0.015f * height, 0f);
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Min(1800f, FieldWidth * density);
            var velocity = particles.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(rise * height * 0.6f, rise * height);
            _properties ??= new MaterialPropertyBlock();
            _properties.SetFloat("_GroundY", transform.position.y);
            _properties.SetFloat("_FieldHeight", height);
            _properties.SetFloat("_PreviewTime", -1f);
            particles.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(_properties);

            // Populate a stationary hazard immediately; this does not advance its gameplay lifetime.
            particles.Simulate(1.6f, false, true, false);
            particles.Play(false);
        }

        private static void Stop(ParticleSystem particles)
        {
            if (particles != null)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() => Clear();
    }
}
