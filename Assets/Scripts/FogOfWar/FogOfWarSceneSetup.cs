using UnityEngine;

/// <summary>
/// Настройки тумана на сцене: asset в Settings имеет приоритет над Resources.
/// </summary>
[DefaultExecutionOrder(300)]
public class FogOfWarSceneSetup : MonoBehaviour
{
    [SerializeField] private FogOfWarSettings settings;

    public FogOfWarSettings AssignedSettings => settings;

    private void Start()
    {
        // Страховка: если что-то перезаписало settings до Start — вернуть сценовый asset.
        if (settings == null || FogOfWarManager.Instance == null) return;
        if (FogOfWarManager.Instance.Settings != settings)
            FogOfWarManager.Instance.ApplySettings(settings);
    }
}
