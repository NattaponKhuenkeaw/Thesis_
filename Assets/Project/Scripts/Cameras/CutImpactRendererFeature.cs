using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Uses URP's stock fullscreen copy/pass only while an action requests a cut impact.</summary>
public sealed class CutImpactRendererFeature : FullScreenPassRendererFeature
{
    private static readonly int CenterTimeId = Shader.PropertyToID("_ImpactCenterTime");
    private static readonly int WaveId = Shader.PropertyToID("_ImpactWave");
    private static readonly int DetailId = Shader.PropertyToID("_ImpactDetail");
    private static readonly int CutEndpointsId = Shader.PropertyToID("_ImpactCut");
    private static readonly int CutShapeId = Shader.PropertyToID("_ImpactShape");
    private Material _effectMaterial;
    private Material _sourceMaterial;
    private ChromaticAberration _suppressedChromatic;
    private float _previousChromaticIntensity;
    private Camera _suppressedCamera;

    public override void Create()
    {
        RestoreChromatic();
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        CoreUtils.Destroy(_effectMaterial);
        _effectMaterial = null;
        _sourceMaterial = null;
        injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
        fetchColorBuffer = true;
        // Renderer2D uses the Color input flag to allocate an intermediate scene target.
        // Its opaque-texture API is unsupported; URP's fullscreen fetch copies the actual 2D scene.
        requirements = ScriptableRenderPassInput.Color;
        bindDepthStencilAttachment = false;
        base.Create();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        RestoreChromatic();
        if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
        if (CutImpactVfxSource.ShouldSuppressChromatic(camera))
        {
            // Change only this camera's evaluated runtime stack, then restore after its render.
            var chromatic = VolumeManager.instance.stack.GetComponent<ChromaticAberration>();
            if (chromatic != null)
            {
                _suppressedChromatic = chromatic; _previousChromaticIntensity = chromatic.intensity.value;
                _suppressedCamera = camera; chromatic.intensity.value = 0f;
            }
        }
        if (passMaterial == null) return;
        if (!ProceduralSlashField.TryGetCutImpact(camera, out var impact)
            && !CutImpactVfxSource.TryGetImpact(camera, out impact)) return;

        if (_effectMaterial == null || _sourceMaterial != passMaterial)
        {
            CoreUtils.Destroy(_effectMaterial);
            _sourceMaterial = passMaterial;
            _effectMaterial = new Material(passMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }
        _effectMaterial.SetVector(CenterTimeId, impact.centerTime);
        _effectMaterial.SetVector(WaveId, impact.wave);
        _effectMaterial.SetVector(DetailId, impact.detail);
        _effectMaterial.SetVector(CutEndpointsId, impact.cutEndpoints);
        _effectMaterial.SetVector(CutShapeId, impact.cutShape);
        // The stock pass captures the transient material; keep the serialized authoring asset untouched.
        Material source = passMaterial;
        try
        {
            passMaterial = _effectMaterial;
            base.AddRenderPasses(renderer, ref renderingData);
        }
        finally { passMaterial = source; }
    }

    protected override void Dispose(bool disposing)
    {
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreChromatic();
        CoreUtils.Destroy(_effectMaterial);
        _effectMaterial = null;
        _sourceMaterial = null;
        base.Dispose(disposing);
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _suppressedCamera) RestoreChromatic();
    }

    private void RestoreChromatic()
    {
        if (_suppressedChromatic != null) _suppressedChromatic.intensity.value = _previousChromaticIntensity;
        _suppressedChromatic = null; _suppressedCamera = null;
    }
}
