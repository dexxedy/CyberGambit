using Mission2;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FogCellState
{
    Unexplored,
    Explored,
    Visible
}

/// <summary>
/// Синглтон тумана войны: две R8-текстуры (explored / visible), штампы зрения Player1.
/// </summary>
[DefaultExecutionOrder(50)]
public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    private static readonly string[] MissionSceneNames = { "mission1", "mission2" };

    [SerializeField] private FogOfWarSettings settings;

    private Texture2D exploredTexture;
    private Texture2D visibleTexture;
    private Texture2D displayExploredTexture;
    private Texture2D displayVisibleTexture;
    private byte[] exploredPixels;
    private byte[] visiblePixels;
    private byte[] displayExploredPixels;
    private byte[] displayVisiblePixels;
    private byte[] botSpectatorExploredPixels;
    private Unit botSpectatorVisionUnit;
    private int resolution;
    private bool initialized;

    public FogOfWarSettings Settings => settings;
    public Texture2D ExploredTexture => exploredTexture;
    public Texture2D VisibleTexture => visibleTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneCallbacks()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySetupMissionScene(scene.name);
    }

    private static void TrySetupMissionScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        bool isMission = false;
        foreach (string n in MissionSceneNames)
        {
            if (sceneName.Equals(n, System.StringComparison.OrdinalIgnoreCase))
            {
                isMission = true;
                break;
            }
        }

        if (!isMission) return;

        // Приоритет: FogOfWarSceneSetup на сцене → Resources по имени миссии → Default.
        FogOfWarSettings sceneSettings = TryGetSceneAssignedSettings();
        if (sceneSettings == null)
            sceneSettings = ResolveSettingsForScene(sceneName);

        if (Instance == null)
            EnsureExists(sceneSettings);
        else if (sceneSettings != null)
            Instance.ApplySettings(sceneSettings);

        FogWarIntelTracker.EnsureExists();
        TagMissionObjectives();
        AutoAttachEnemyVisibility();

        if (Instance != null)
            Instance.ResetFog();
        FogWarIntelTracker.Instance?.ResetIntel();
    }

    /// <summary>Asset из FogOfWarSceneSetup на активной сцене (если назначен в Inspector).</summary>
    private static FogOfWarSettings TryGetSceneAssignedSettings()
    {
        FogOfWarSceneSetup[] setups = Object.FindObjectsByType<FogOfWarSceneSetup>(FindObjectsInactive.Include);
        foreach (FogOfWarSceneSetup setup in setups)
        {
            if (setup == null) continue;
            FogOfWarSettings assigned = setup.AssignedSettings;
            if (assigned != null)
                return assigned;
        }

        return null;
    }

    private static FogOfWarSettings ResolveSettingsForScene(string sceneName)
    {
        if (sceneName.Equals("mission2", System.StringComparison.OrdinalIgnoreCase))
        {
            FogOfWarSettings m2 = Resources.Load<FogOfWarSettings>("FogOfWar/Mission2FogOfWarSettings");
            if (m2 != null) return m2;
        }

        if (sceneName.Equals("mission1", System.StringComparison.OrdinalIgnoreCase))
        {
            FogOfWarSettings m1 = Resources.Load<FogOfWarSettings>("FogOfWar/Mission1FogOfWarSettings");
            if (m1 != null) return m1;
        }

        return Resources.Load<FogOfWarSettings>("FogOfWar/DefaultFogOfWarSettings");
    }

    private static void TagMissionObjectives()
    {
        Mission1FlagZone[] flags = Object.FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Include);
        foreach (Mission1FlagZone f in flags)
            EnsureExempt(f != null ? f.gameObject : null);

        DestructibleObjective[] objs = Object.FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Include);
        foreach (DestructibleObjective o in objs)
            EnsureExempt(o != null ? o.gameObject : null);

        Mission2.Mission2DefenseTerminal[] terminals =
            Object.FindObjectsByType<Mission2.Mission2DefenseTerminal>(FindObjectsInactive.Include);
        foreach (var t in terminals)
            EnsureExempt(t != null ? t.gameObject : null);
    }

    private static void EnsureExempt(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<FogWarExempt>() == null)
            go.AddComponent<FogWarExempt>();
    }

    private static void AutoAttachEnemyVisibility()
    {
        Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include);
        foreach (Unit u in units)
        {
            if (u == null || u.owner != Player.Player2) continue;
            if (u.GetComponent<FogWarUnitVisibility>() == null)
                u.gameObject.AddComponent<FogWarUnitVisibility>();
        }
    }

    public void ApplySettings(FogOfWarSettings newSettings)
    {
        if (newSettings == null) return;
        settings = newSettings;
        initialized = false;
        InitializeTextures();
    }

    public static void EnsureExists(FogOfWarSettings settingsOverride = null)
    {
        if (Instance != null) return;

        FogOfWarSettings loaded = settingsOverride;
        if (loaded == null)
            loaded = Resources.Load<FogOfWarSettings>("FogOfWar/DefaultFogOfWarSettings");

        GameObject go = new GameObject("FogOfWar");
        var mgr = go.AddComponent<FogOfWarManager>();
        if (loaded != null)
            mgr.ApplySettings(loaded);
        go.AddComponent<FogOfWarOverlay>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (settings == null)
        {
            FogOfWarSettings sceneAssigned = TryGetSceneAssignedSettings();
            settings = sceneAssigned != null
                ? sceneAssigned
                : Resources.Load<FogOfWarSettings>("FogOfWar/DefaultFogOfWarSettings");
        }

        InitializeTextures();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (exploredTexture != null) Destroy(exploredTexture);
        if (visibleTexture != null) Destroy(visibleTexture);
        if (displayExploredTexture != null) Destroy(displayExploredTexture);
        if (displayVisibleTexture != null) Destroy(displayVisibleTexture);
    }

    private void LateUpdate()
    {
        if (!ShouldRunFog()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsArmyDeploymentPhase())
            return;
        if (!initialized) InitializeTextures();

        RunVisionPass();

        if (IsBotSpectatorFogActive())
        {
            if (botSpectatorVisionUnit == null)
                TryAssignBotSpectatorVisionUnit();
        }
        else if (botSpectatorVisionUnit != null)
            EndBotSpectatorVision();

        SyncDisplayFogTextures();
        if (FogWarIntelTracker.Instance != null)
            FogWarIntelTracker.Instance.UpdateFromFog();
        FogWarUnitVisibility.RefreshAll();
    }

    public void BeginBotSpectatorVision(Unit botUnit)
    {
        if (!initialized || botUnit == null || botUnit.owner != Player.Player2) return;
        if (botSpectatorVisionUnit == botUnit) return;

        botSpectatorVisionUnit = botUnit;
        if (botSpectatorExploredPixels == null || botSpectatorExploredPixels.Length != exploredPixels.Length)
            botSpectatorExploredPixels = new byte[exploredPixels.Length];
        System.Array.Clear(botSpectatorExploredPixels, 0, botSpectatorExploredPixels.Length);
    }

    public void EndBotSpectatorVision()
    {
        botSpectatorVisionUnit = null;
    }

    private void TryAssignBotSpectatorVisionUnit()
    {
        Unit candidate = null;
        if (CameraManager.Instance != null)
            candidate = CameraManager.Instance.GetFollowedBotUnit();
        if (candidate == null && BotController.Instance != null)
            candidate = BotController.Instance.GetPickedActingUnitForPresentation();
        if (candidate != null && candidate.GetHealth() > 0)
            BeginBotSpectatorVision(candidate);
    }

    /// <summary>
    /// Во время хода бота: на экране туман с зрением бота (только визуал), логика игрока — в explored/visible.
    /// </summary>
    public bool IsBotSpectatorFogActive()
    {
        if (!ShouldRunFog()) return false;
        if (GameManager.Instance == null || !GameManager.Instance.IsBotTurn()) return false;
        return CameraManager.Instance == null || !CameraManager.Instance.IsActionMode();
    }

    public bool ShouldRunFog()
    {
        if (settings == null) return false;
        if (GameManager.Instance == null) return false;
        if (GameManager.Instance.IsArmyDeploymentPhase()) return false;
        return GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot;
    }

    private void InitializeTextures()
    {
        if (settings == null) return;

        resolution = Mathf.Max(32, settings.textureResolution);
        exploredPixels = new byte[resolution * resolution];
        visiblePixels = new byte[resolution * resolution];

        exploredTexture = CreateFogTexture("FogExplored");
        visibleTexture = CreateFogTexture("FogVisible");
        displayExploredPixels = new byte[resolution * resolution];
        displayVisiblePixels = new byte[resolution * resolution];
        displayExploredTexture = CreateFogTexture("FogExploredDisplay");
        displayVisibleTexture = CreateFogTexture("FogVisibleDisplay");
        initialized = true;
        ResetFog();
    }

    private Texture2D CreateFogTexture(string name)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return tex;
    }

    public void ResetFog()
    {
        if (!initialized && settings != null)
            InitializeTextures();
        if (!initialized) return;

        System.Array.Clear(exploredPixels, 0, exploredPixels.Length);
        System.Array.Clear(visiblePixels, 0, visiblePixels.Length);
        PushTexturesToGpu();
    }

    public void ClearVisiblePass()
    {
        if (!initialized) return;
        System.Array.Clear(visiblePixels, 0, visiblePixels.Length);
    }

    public void RunVisionPass()
    {
        if (!initialized || settings == null) return;

        ClearVisiblePass();

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        bool actionMode = CameraManager.Instance != null && CameraManager.Instance.IsActionMode();
        Unit controlled = actionMode && CameraManager.Instance != null
            ? CameraManager.Instance.GetCurrentControlledUnit()
            : null;
        Camera actionCam = actionMode && CameraManager.Instance != null
            ? CameraManager.Instance.GetActionCamera()
            : null;

        bool soft = settings.useSoftStampEdges;
        int feather = settings.stampFeatherPixels;

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit.GetHealth() <= 0) continue;
            if (unit.owner != Player.Player1) continue;

            Vector3 pos = unit.transform.position;
            Vector3 forward = unit.transform.forward;
            if (actionMode && unit == controlled && actionCam != null)
            {
                Vector3 camFwd = actionCam.transform.forward;
                camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.0001f)
                    forward = camFwd.normalized;
            }

            FogWarVisionStamper.StampDisk(
                exploredPixels, visiblePixels, resolution,
                settings.worldMinXZ, settings.worldMaxXZ,
                pos, settings.nearRevealRadiusMeters, soft, feather);

            FogWarVisionStamper.StampCone(
                exploredPixels, visiblePixels, resolution,
                settings.worldMinXZ, settings.worldMaxXZ,
                pos, forward,
                settings.whiteConeRangeMeters, settings.whiteConeFovDegrees,
                soft, feather);
        }
    }

    private void SyncDisplayFogTextures()
    {
        if (!initialized) return;

        if (IsBotSpectatorFogActive() && botSpectatorVisionUnit != null)
            SyncDisplayFogFromActingBot(botSpectatorVisionUnit);
        else
        {
            System.Array.Copy(exploredPixels, displayExploredPixels, exploredPixels.Length);
            System.Array.Copy(visiblePixels, displayVisiblePixels, visiblePixels.Length);
        }

        displayExploredTexture.LoadRawTextureData(displayExploredPixels);
        displayExploredTexture.Apply(false, false);
        displayVisibleTexture.LoadRawTextureData(displayVisiblePixels);
        displayVisibleTexture.Apply(false, false);
    }

    private void SyncDisplayFogFromActingBot(Unit botUnit)
    {
        if (botUnit == null || botUnit.GetHealth() <= 0) return;
        if (botSpectatorVisionUnit != botUnit)
            BeginBotSpectatorVision(botUnit);

        if (botSpectatorExploredPixels == null)
            botSpectatorExploredPixels = new byte[exploredPixels.Length];

        System.Array.Clear(displayVisiblePixels, 0, displayVisiblePixels.Length);
        System.Array.Copy(botSpectatorExploredPixels, displayExploredPixels, botSpectatorExploredPixels.Length);

        StampVisionForUnit(botUnit, displayExploredPixels, displayVisiblePixels);

        System.Array.Copy(displayExploredPixels, botSpectatorExploredPixels, botSpectatorExploredPixels.Length);
    }

    private void StampVisionForUnit(Unit unit, byte[] explored, byte[] visible)
    {
        if (!initialized || settings == null || unit == null) return;

        bool soft = settings.useSoftStampEdges;
        int feather = settings.stampFeatherPixels;
        Vector3 pos = unit.transform.position;
        Vector3 forward = unit.transform.forward;

        FogWarVisionStamper.StampDisk(
            explored, visible, resolution,
            settings.worldMinXZ, settings.worldMaxXZ,
            pos, settings.nearRevealRadiusMeters, soft, feather);

        FogWarVisionStamper.StampCone(
            explored, visible, resolution,
            settings.worldMinXZ, settings.worldMaxXZ,
            pos, forward,
            settings.whiteConeRangeMeters, settings.whiteConeFovDegrees,
            soft, feather);
    }

    private void PushTexturesToGpu()
    {
        exploredTexture.LoadRawTextureData(exploredPixels);
        exploredTexture.Apply(false, false);
        visibleTexture.LoadRawTextureData(visiblePixels);
        visibleTexture.Apply(false, false);
        SyncDisplayFogTextures();
    }

    public bool WorldToCell(Vector3 worldPos, out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;
        if (!initialized || settings == null) return false;
        return FogWarVisionStamper.WorldToCell(
            worldPos, settings.worldMinXZ, settings.worldMaxXZ, resolution, out cellX, out cellY);
    }

    public FogCellState GetCellState(Vector3 worldPos)
    {
        if (!WorldToCell(worldPos, out int x, out int y))
            return FogCellState.Unexplored;

        int idx = x + y * resolution;
        if (visiblePixels[idx] > 0)
            return FogCellState.Visible;
        if (exploredPixels[idx] > 0)
            return FogCellState.Explored;
        return FogCellState.Unexplored;
    }

    public bool IsWorldPositionVisible(Vector3 worldPos)
    {
        return GetCellState(worldPos) == FogCellState.Visible;
    }

    public bool IsWorldPositionExplored(Vector3 worldPos)
    {
        FogCellState s = GetCellState(worldPos);
        return s == FogCellState.Explored || s == FogCellState.Visible;
    }

    public FogCellState GetDisplayCellState(Vector3 worldPos)
    {
        if (!WorldToCell(worldPos, out int x, out int y))
            return FogCellState.Unexplored;

        int idx = x + y * resolution;
        if (displayVisiblePixels[idx] > 0)
            return FogCellState.Visible;
        if (displayExploredPixels[idx] > 0)
            return FogCellState.Explored;
        return FogCellState.Unexplored;
    }

    public bool IsWorldPositionVisibleOnDisplay(Vector3 worldPos)
    {
        return GetDisplayCellState(worldPos) == FogCellState.Visible;
    }

    public Vector3 GetWorldBoundsCenter()
    {
        if (settings == null) return Vector3.zero;
        float cx = (settings.worldMinXZ.x + settings.worldMaxXZ.x) * 0.5f;
        float cz = (settings.worldMinXZ.y + settings.worldMaxXZ.y) * 0.5f;
        return new Vector3(cx, settings.overlayHeightY, cz);
    }

    public Vector3 GetWorldBoundsSize()
    {
        if (settings == null) return Vector3.one * 16f;
        float sx = settings.worldMaxXZ.x - settings.worldMinXZ.x;
        float sz = settings.worldMaxXZ.y - settings.worldMinXZ.y;
        float pad = settings.overlayScalePadding;
        return new Vector3(sx + pad * 2f, 1f, sz + pad * 2f);
    }

    public enum FogOfWarMaterialProfile
    {
        TacticalOverlay,
        TacticalScreen,
        ActionScreen
    }

    public void ApplyToMaterial(Material mat, FogOfWarMaterialProfile profile)
    {
        if (mat == null || settings == null) return;
        if (exploredTexture == null || visibleTexture == null) return;

        mat.SetTexture("_ExploredTex", displayExploredTexture);
        mat.SetTexture("_VisibleTex", displayVisibleTexture);
        mat.SetVector("_WorldMinXZ", new Vector4(settings.worldMinXZ.x, settings.worldMinXZ.y, 0f, 0f));
        mat.SetVector("_WorldMaxXZ", new Vector4(settings.worldMaxXZ.x, settings.worldMaxXZ.y, 0f, 0f));

        switch (profile)
        {
            case FogOfWarMaterialProfile.TacticalOverlay:
                mat.SetFloat("_UnexploredAlpha", settings.unexploredFogAlpha);
                mat.SetFloat("_ExploredAlpha", settings.exploredFogAlpha);
                mat.SetColor("_FogColor", settings.tacticalFogColor);
                mat.SetFloat("_VisibleFade", settings.tacticalVisibleFade);
                break;

            case FogOfWarMaterialProfile.TacticalScreen:
                mat.SetFloat("_UnexploredAlpha", settings.tacticalScreenUnexploredAlpha);
                mat.SetFloat("_ExploredAlpha", settings.tacticalScreenExploredAlpha);
                mat.SetColor("_FogColor", settings.tacticalScreenFogColor);
                mat.SetFloat("_HeightStart", settings.tacticalScreenHeightStart);
                mat.SetFloat("_HeightEnd", settings.tacticalScreenHeightEnd);
                mat.SetFloat("_SkyDepthThreshold", settings.tacticalScreenSkyDepthThreshold);
                mat.SetFloat("_VisibleFade", settings.tacticalScreenVisibleFade);
                mat.SetFloat("_ExploredFade", settings.tacticalScreenExploredFade);
                break;

            case FogOfWarMaterialProfile.ActionScreen:
                mat.SetFloat("_UnexploredAlpha", settings.unexploredFogAlpha);
                mat.SetFloat("_ExploredAlpha", settings.exploredFogAlpha);
                mat.SetColor("_FogColor", settings.actionFogColor);
                mat.SetFloat("_HeightStart", settings.actionFogHeightStart);
                mat.SetFloat("_HeightEnd", settings.actionFogHeightEnd);
                mat.SetFloat("_SkyDepthThreshold", settings.actionSkyDepthThreshold);
                mat.SetFloat("_VisibleFade", settings.actionVisibleFade);
                mat.SetFloat("_ExploredFade", settings.actionExploredFade);
                break;
        }

        ApplyFogAppearance(mat);
    }

    private void ApplyFogAppearance(Material mat)
    {
        bool useAlbedo = settings.fogAppearance == FogAppearanceMode.TexturedAlbedo
                         && settings.fogAlbedoTexture != null;
        mat.SetFloat("_UseFogAlbedoTex", useAlbedo ? 1f : 0f);
        mat.SetVector("_FogAlbedoTile", new Vector4(settings.fogAlbedoTileX, settings.fogAlbedoTileY, 0f, 0f));
        mat.SetColor("_FogAlbedoTint", settings.fogAlbedoTint);

        if (useAlbedo)
            mat.SetTexture("_FogAlbedoTex", settings.fogAlbedoTexture);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (settings == null) return;
        Vector3 min = new Vector3(settings.worldMinXZ.x, 0f, settings.worldMinXZ.y);
        Vector3 max = new Vector3(settings.worldMaxXZ.x, 0f, settings.worldMaxXZ.y);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(center, new Vector3(size.x, 0.1f, size.z));
    }
#endif
}
