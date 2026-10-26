using UnityEngine;

namespace Project.Progression.RestRoom
{
    /// <summary>Presentation only: hover and drifting mist for room landmarks.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FloatingRoomTriangle : MonoBehaviour
    {
        [SerializeField] private Color triangleColor = new Color(0.56f, 0.96f, 0.18f, 1f);
        [SerializeField] private Color smokeColor = new Color(1f, 0.12f, 0.46f, 0.8f);
        [SerializeField, Min(0f)] private float hoverHeight = 0.12f;
        [SerializeField, Min(0.1f)] private float hoverPeriod = 2.8f;
        [SerializeField] private Material smokeMaterial;

        private Vector3 restPosition;
        private float elapsed;
        private ParticleSystem smoke;

        private void OnEnable()
        {
            restPosition = transform.localPosition;
            elapsed = 0f;
            var sprite = GetComponent<SpriteRenderer>();
            sprite.color = triangleColor;
            if (smokeMaterial == null)
                smokeMaterial = Resources.Load<Material>("EnemyLifecycleParticles");
            if (smokeMaterial == null)
            {
                Debug.LogWarning("[FloatingRoomTriangle] Missing smoke material.", this);
                return;
            }

            var host = new GameObject("Pink Mist");
            host.transform.SetParent(transform, false);
            smoke = host.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 64;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
            main.startSpeed = 0f;
            float visualHeight = Mathf.Max(0.8f, sprite.bounds.size.y);
            main.startSize = new ParticleSystem.MinMaxCurve(visualHeight * 0.35f, visualHeight * 0.6f);
            main.startColor = smokeColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = smoke.emission;
            emission.rateOverTime = 14f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.09f, 0.09f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var fade = smoke.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.65f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var spin = smoke.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.sortingLayerID = sprite.sortingLayerID;
            // The city foreground is order 1; order -1 hid the mist behind buildings.
            renderer.sortingOrder = Mathf.Max(2, sprite.sortingOrder + 1);
            renderer.maxParticleSize = 1f;
            smoke.Simulate(1.5f, true, true, true);
            smoke.Play();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.localPosition = restPosition + Vector3.up *
                (Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, hoverPeriod)) * hoverHeight);
        }

        private void OnDisable()
        {
            transform.localPosition = restPosition;
            if (smoke == null) return;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(smoke.gameObject);
            smoke = null;
        }
    }
}
