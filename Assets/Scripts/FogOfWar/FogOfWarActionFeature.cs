using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fullscreen FoW: depth → world XZ → explored/visible текстуры.
/// Экшен (FPS) и тактика (orbital camera) — разные профили материала.
/// </summary>
public class FogOfWarActionFeature : ScriptableRendererFeature
{
    [SerializeField] private Material fogMaterial;

    private FogOfWarScreenPass actionFogPass;
    private FogOfWarScreenPass tacticalFogPass;

    public override void Create()
    {
        actionFogPass = new FogOfWarScreenPass("FogOfWarScreenAction");
        tacticalFogPass = new FogOfWarScreenPass("FogOfWarScreenTactical");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fogMaterial == null || actionFogPass == null || tacticalFogPass == null)
            return;

        Camera cam = renderingData.cameraData.camera;
        if (cam == null || renderingData.cameraData.cameraType != CameraType.Game)
            return;

        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.ShouldRunFog())
            return;

        if (CameraManager.Instance == null)
            return;

        bool actionMode = CameraManager.Instance.IsActionMode();
        Camera actionCam = CameraManager.Instance.GetActionCamera();
        Camera tacticalCam = CameraManager.Instance.GetTacticalCamera();

        if (actionMode)
        {
            if (actionCam == null || cam != actionCam)
                return;

            EnqueueScreenPass(renderer, actionFogPass,
                FogOfWarManager.FogOfWarMaterialProfile.ActionScreen,
                "FogOfWar Action",
                RenderPassEvent.AfterRenderingTransparents,
                requiresIntermediateTexture: true);
            return;
        }

        if (actionCam != null && cam == actionCam)
            return;

        if (tacticalCam == null || cam != tacticalCam)
            return;

        FogOfWarSettings settings = FogOfWarManager.Instance.Settings;
        if (settings != null && settings.disableTacticalScreenFog)
            return;

        // До transparents: ground overlay (quad) рисуется поверх, не попадает в blit.
        EnqueueScreenPass(renderer, tacticalFogPass,
            FogOfWarManager.FogOfWarMaterialProfile.TacticalScreen,
            "FogOfWar Tactical",
            RenderPassEvent.BeforeRenderingTransparents,
            requiresIntermediateTexture: false);
    }

    private void EnqueueScreenPass(
        ScriptableRenderer renderer,
        FogOfWarScreenPass pass,
        FogOfWarManager.FogOfWarMaterialProfile profile,
        string label,
        RenderPassEvent injectionPoint,
        bool requiresIntermediateTexture)
    {
        pass.Setup(fogMaterial, profile, label);
        pass.renderPassEvent = injectionPoint;
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        pass.requiresIntermediateTexture = requiresIntermediateTexture;
        renderer.EnqueuePass(pass);
    }

    private sealed class FogOfWarScreenPass : ScriptableRenderPass
    {
        private Material material;
        private FogOfWarManager.FogOfWarMaterialProfile profile;
        private string blitPassName;

        public FogOfWarScreenPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }

        public void Setup(Material mat, FogOfWarManager.FogOfWarMaterialProfile materialProfile, string label)
        {
            material = mat;
            profile = materialProfile;
            blitPassName = label;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.cameraColor.IsValid())
                return;

            if (FogOfWarManager.Instance != null)
                FogOfWarManager.Instance.ApplyToMaterial(material, profile);

            TextureHandle source = resourceData.activeColorTexture;

            var targetDesc = renderGraph.GetTextureDesc(source);
            targetDesc.name = "_FogOfWarScreenTemp";
            targetDesc.clearBuffer = false;
            TextureHandle temp = renderGraph.CreateTexture(targetDesc);

            renderGraph.AddBlitPass(source, temp, Vector2.one, Vector2.zero, passName: "FogOfWar Copy Color");

            var blitParams = new RenderGraphUtils.BlitMaterialParameters(temp, source, material, 0);
            renderGraph.AddBlitPass(blitParams, passName: blitPassName);
        }
    }
}
