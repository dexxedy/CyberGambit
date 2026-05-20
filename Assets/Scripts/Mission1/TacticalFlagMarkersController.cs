using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// В тактике показывает буквы A/B/C над флагами миссии 1 и раскрашивает их по владельцу.
/// </summary>
public class TacticalFlagMarkersController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform markersRoot;
    [SerializeField] private TacticalFlagMarker markerPrefab;
    [SerializeField] private float worldOffsetY = 1.5f;

    [Header("Colors")]
    [SerializeField] private Color neutralColor = new Color(1f, 0.2f, 0.2f, 1f);  // изначально красный
    [SerializeField] private Color player1Color = new Color(0.2f, 0.6f, 1f, 1f);   // синий (игрок)
    [SerializeField] private Color player2Color = new Color(1f, 0.25f, 0.25f, 1f); // красный (бот/враг)

    [Header("Options")]
    [SerializeField] private bool hideMarkersWhenBehindCamera = true;

    private readonly Dictionary<Mission1FlagZone, TacticalFlagMarker> markersByFlag =
        new Dictionary<Mission1FlagZone, TacticalFlagMarker>();

    private void LateUpdate()
    {
        if (CameraManager.Instance == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
        {
            if (markersRoot != null)
                markersRoot.gameObject.SetActive(false);
            return;
        }
        if (markersRoot == null || markerPrefab == null) return;

        bool tactical = !CameraManager.Instance.IsActionMode();
        markersRoot.gameObject.SetActive(tactical);
        if (!tactical) return;

        Camera tacCam = CameraManager.Instance.GetTacticalCamera();
        if (tacCam == null) return;

        SyncFlags();
        UpdateMarkers(tacCam);
    }

    private void SyncFlags()
    {
        Mission1FlagZone[] flags = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        if (flags == null) flags = new Mission1FlagZone[0];

        // Создать маркеры для новых флагов
        foreach (Mission1FlagZone f in flags)
        {
            if (f == null) continue;
            if (markersByFlag.ContainsKey(f)) continue;
            TacticalFlagMarker m = Instantiate(markerPrefab, markersRoot);
            m.SetLetter(IndexToLetter(f.FlagIndex));
            markersByFlag[f] = m;
        }

        // Удалить маркеры для удалённых флагов
        HashSet<Mission1FlagZone> alive = flags.Where(x => x != null).ToHashSet();
        List<Mission1FlagZone> toRemove = null;
        foreach (var kv in markersByFlag)
        {
            if (kv.Key == null || !alive.Contains(kv.Key))
            {
                toRemove ??= new List<Mission1FlagZone>();
                toRemove.Add(kv.Key);
            }
        }
        if (toRemove != null)
        {
            foreach (Mission1FlagZone dead in toRemove)
            {
                if (markersByFlag.TryGetValue(dead, out TacticalFlagMarker m) && m != null)
                    Destroy(m.gameObject);
                markersByFlag.Remove(dead);
            }
        }
    }

    private void UpdateMarkers(Camera tacCam)
    {
        foreach (var kv in markersByFlag)
        {
            Mission1FlagZone flag = kv.Key;
            TacticalFlagMarker marker = kv.Value;
            if (flag == null || marker == null) continue;

            RectTransform rt = marker.transform as RectTransform;
            if (rt == null) continue;

            Vector3 world = flag.transform.position + Vector3.up * worldOffsetY;
            Vector3 screen = tacCam.WorldToScreenPoint(world);

            bool behind = hideMarkersWhenBehindCamera && screen.z < 0.1f;
            marker.gameObject.SetActive(!behind);
            if (behind) continue;

            rt.position = new Vector3(screen.x, screen.y, 0f);
            marker.SetColor(OwnerToColor(flag.CurrentOwner));
        }
    }

    private Color OwnerToColor(Mission1FlagZone.FlagOwner owner)
    {
        switch (owner)
        {
            case Mission1FlagZone.FlagOwner.Player1: return player1Color;
            case Mission1FlagZone.FlagOwner.Player2: return player2Color;
            default: return neutralColor;
        }
    }

    private static char IndexToLetter(int flagIndex)
    {
        // 0->A, 1->B, 2->C, иначе: A + index
        int i = Mathf.Clamp(flagIndex, 0, 25);
        return (char)('A' + i);
    }
}

