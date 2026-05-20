using UnityEngine;

public enum FogAppearanceMode
{
    FlatColor = 0,
    TexturedAlbedo = 1
}

[CreateAssetMenu(fileName = "FogOfWarSettings", menuName = "CyberGambit/Fog Of War Settings")]
public class FogOfWarSettings : ScriptableObject
{
    [Header("World bounds (XZ)")]
    public Vector2 worldMinXZ = new Vector2(0f, 0f);
    public Vector2 worldMaxXZ = new Vector2(16f, 16f);

    [Header("Texture")]
    [Min(32)]
    public int textureResolution = 256;

    [Header("White vision")]
    [Min(0.1f)]
    public float whiteConeRangeMeters = 18f;
    [Range(10f, 180f)]
    public float whiteConeFovDegrees = 90f;
    [Min(0f)]
    public float nearRevealRadiusMeters = 2.5f;

    [Header("Fog appearance (all passes)")]
    [Tooltip("FlatColor — как раньше, один цвет. TexturedAlbedo — тайл текстуры по карте (нужна Fog Albedo Texture).")]
    public FogAppearanceMode fogAppearance = FogAppearanceMode.FlatColor;
    public Texture2D fogAlbedoTexture;
    [Min(0.01f)]
    public float fogAlbedoTileX = 4f;
    [Min(0.01f)]
    public float fogAlbedoTileY = 4f;
    [Tooltip("Умножает текстуру; в FlatColor это основной цвет тумана.")]
    public Color fogAlbedoTint = Color.white;

    [Header("Overlay visual")]
    [Range(0f, 1f)]
    public float unexploredFogAlpha = 0.92f;
    [Range(0f, 1f)]
    public float exploredFogAlpha = 0.55f;
    public float overlayHeightY = 0.15f;
    [Min(0f)]
    public float overlayScalePadding = 0.05f;

    [Header("Stamp soft edges")]
    public bool useSoftStampEdges = true;
    [Range(0, 4)]
    public int stampFeatherPixels = 1;

    [Header("Tactical overlay (ground quad)")]
    [Tooltip("Выключить плоский quad-туман на земле в тактике.")]
    public bool disableTacticalOverlayFog = false;
    public Color tacticalFogColor = new Color(0.02f, 0.04f, 0.07f, 1f);
    [Min(0.001f)]
    public float tacticalVisibleFade = 0.12f;

    [Header("Tactical Mode (Screen Fog on geometry)")]
    [Tooltip("Fullscreen depth fog на tactical camera поверх ground overlay.")]
    public bool disableTacticalScreenFog = false;
    public Color tacticalScreenFogColor = new Color(0.02f, 0.04f, 0.07f, 1f);
    [Range(0f, 1f)]
    public float tacticalScreenUnexploredAlpha = 0.62f;
    [Range(0f, 1f)]
    public float tacticalScreenExploredAlpha = 0.32f;
    public float tacticalScreenHeightStart = 500f;
    public float tacticalScreenHeightEnd = -500f;
    [Range(0.9f, 1f)]
    public float tacticalScreenSkyDepthThreshold = 0.999f;
    [Min(0.001f)]
    public float tacticalScreenVisibleFade = 0.14f;
    [Min(0.001f)]
    public float tacticalScreenExploredFade = 0.09f;

    [Header("Action Mode (Screen Fog)")]
    public Color actionFogColor = new Color(0.02f, 0.05f, 0.09f, 1f);
    public float actionFogHeightStart = -1f;
    public float actionFogHeightEnd = 12f;
    [Range(0.9f, 1f)]
    public float actionSkyDepthThreshold = 0.99f;
    [Min(0.001f)]
    public float actionVisibleFade = 0.12f;
    [Min(0.001f)]
    public float actionExploredFade = 0.08f;
}
