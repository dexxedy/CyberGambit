using UnityEngine;

[DefaultExecutionOrder(100)]
public class HubSceneBootstrap : MonoBehaviour
{
    [SerializeField] private Unit hubAvatarPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private MissionSelectUI missionSelectUI;
    [SerializeField] private float hubMoveBudgetMeters = 99999f;

    public void Initialize(Unit avatar, Transform spawn, MissionSelectUI ui)
    {
        hubAvatarPrefab = avatar;
        spawnPoint = spawn;
        missionSelectUI = ui;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        DisableTacticalSystems();

        Unit avatar = Object.FindAnyObjectByType<Unit>();
        if (avatar == null && hubAvatarPrefab != null)
        {
            avatar = Instantiate(hubAvatarPrefab, spawnPoint.position, spawnPoint.rotation);
        }

        if (avatar != null)
        {
            avatar.gameObject.tag = "Player";
            avatar.owner = Player.Player1;
            avatar.hubExploreNoCombat = true;
            avatar.SetRemainingMoveMeters(hubMoveBudgetMeters);

            if (CameraManager.Instance != null)
                CameraManager.Instance.EnterHubExploreMode(avatar);

            if (ActionModeUI.Instance != null)
                ActionModeUI.Instance.HideStatsPanels();
        }
    }

    private void DisableTacticalSystems()
    {
        BotController bot = Object.FindAnyObjectByType<BotController>();
        if (bot != null) bot.enabled = false;

        ArmyDeploymentController adc = Object.FindAnyObjectByType<ArmyDeploymentController>();
        if (adc != null) adc.enabled = false;

        DiceRenderUI dice = Object.FindAnyObjectByType<DiceRenderUI>();
        if (dice != null) dice.gameObject.SetActive(false);

        TacticalModeUI tmui = Object.FindAnyObjectByType<TacticalModeUI>();
        if (tmui != null) tmui.gameObject.SetActive(false);

        GridHighlighter gh = Object.FindAnyObjectByType<GridHighlighter>();
        if (gh != null) gh.enabled = false;

        TacticalWorldIconsController twic = Object.FindAnyObjectByType<TacticalWorldIconsController>();
        if (twic != null) twic.gameObject.SetActive(false);
    }
}
