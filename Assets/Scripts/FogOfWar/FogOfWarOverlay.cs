using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(100)]
public class FogOfWarOverlay : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Material fogMaterial;

    private void Awake()
    {
        BuildQuadMesh();
        SetupMaterial();
        RefreshTransformFromSettings();
    }

    private void LateUpdate()
    {
        FogOfWarManager mgr = FogOfWarManager.Instance;
        if (mgr == null || !mgr.ShouldRunFog() || fogMaterial == null)
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            return;
        }

        bool isAction = CameraManager.Instance != null && CameraManager.Instance.IsActionMode();
        FogOfWarSettings settings = mgr.Settings;
        bool overlayEnabled = settings == null || !settings.disableTacticalOverlayFog;
        meshRenderer.enabled = !isAction && overlayEnabled;
        if (isAction || !overlayEnabled)
            return;

        RefreshTransformFromSettings();
        mgr.ApplyToMaterial(fogMaterial, FogOfWarManager.FogOfWarMaterialProfile.TacticalOverlay);
    }

    private void BuildQuadMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh { name = "FogOfWarOverlayQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void SetupMaterial()
    {
        Shader shader = Shader.Find("CyberGambit/FogOfWarOverlay");
        if (shader == null)
        {
            Debug.LogWarning("FogOfWarOverlay: shader CyberGambit/FogOfWarOverlay not found.");
            return;
        }

        fogMaterial = new Material(shader) { name = "FogOfWarOverlay (Runtime)" };
        meshRenderer.sharedMaterial = fogMaterial;
    }

    private void RefreshTransformFromSettings()
    {
        FogOfWarManager mgr = FogOfWarManager.Instance;
        if (mgr == null || mgr.Settings == null) return;

        FogOfWarSettings s = mgr.Settings;
        transform.position = new Vector3(
            (s.worldMinXZ.x + s.worldMaxXZ.x) * 0.5f,
            s.overlayHeightY,
            (s.worldMinXZ.y + s.worldMaxXZ.y) * 0.5f);
        float sx = s.worldMaxXZ.x - s.worldMinXZ.x + s.overlayScalePadding * 2f;
        float sz = s.worldMaxXZ.y - s.worldMinXZ.y + s.overlayScalePadding * 2f;
        transform.localScale = new Vector3(sx, 1f, sz);
    }

    private void OnDestroy()
    {
        if (fogMaterial != null)
            Destroy(fogMaterial);
    }
}
