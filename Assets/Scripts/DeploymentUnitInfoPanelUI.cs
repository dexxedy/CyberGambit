using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель информации о юните при расстановке (справа). Показывается при наведении на карточку (DeploymentCardUI).
/// </summary>
public class DeploymentUnitInfoPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Sprite defaultPanelSprite;

    [Header("Stats")]
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text healthText;

    [Header("Ability")]
    [SerializeField] private TMP_Text abilityNameText;
    [SerializeField] private TMP_Text abilityDescriptionText;
    [SerializeField] private TMP_Text abilityCooldownText;

    private void Awake()
    {
        EnsurePanelRoot();
        Hide();
    }

    private void Start()
    {
        Hide();
    }

    /// <summary>Для UI, собранного в ArmyDeploymentController.BuildDeploymentUnitInfoPanel.</summary>
    public void ConfigureRuntime(GameObject root, Image bg, TMP_Text damageTmp, TMP_Text healthTmp,
        TMP_Text nameTmp, TMP_Text descTmp, TMP_Text cdTmp, Sprite defaultSprite)
    {
        panelRoot = root;
        panelBackground = bg;
        damageText = damageTmp;
        healthText = healthTmp;
        abilityNameText = nameTmp;
        abilityDescriptionText = descTmp;
        abilityCooldownText = cdTmp;
        defaultPanelSprite = defaultSprite;
        Hide();
    }

    public void Show(DeploymentOfferEntry offer)
    {
        EnsurePanelRoot();
        if (panelRoot == null) return;

        Unit unit = offer != null && offer.unitPrefab != null ? offer.unitPrefab.GetComponent<Unit>() : null;
        if (unit == null)
        {
            Hide();
            return;
        }

        if (panelBackground != null)
        {
            Sprite s = offer.infoPanelSprite != null ? offer.infoPanelSprite : defaultPanelSprite;
            if (s != null)
            {
                panelBackground.sprite = s;
                panelBackground.color = Color.white;
                panelBackground.preserveAspect = false;
            }
        }

        Color statValueColor = new Color(1f, 0.35f, 0.45f);

        if (damageText != null)
        {
            damageText.text = unit.Damage.ToString();
            damageText.color = statValueColor;
        }

        if (healthText != null)
        {
            healthText.text = unit.GetHealth().ToString();
            healthText.color = statValueColor;
        }

        if (abilityNameText != null)
            abilityNameText.text = UnitAbilityDisplayTexts.GetAbilityTitle(unit.chessType);

        if (abilityDescriptionText != null)
            abilityDescriptionText.text = UnitAbilityDisplayTexts.GetAbilityDescription(unit.chessType);

        if (abilityCooldownText != null)
            abilityCooldownText.text = UnitAbilityDisplayTexts.GetAbilityCooldownLine(unit);

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        EnsurePanelRoot();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void EnsurePanelRoot()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }
}
