using UnityEngine;

[CreateAssetMenu(fileName = "NewMission", menuName = "CyberGambit/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    public string missionId;
    public string displayName;
    [TextArea(3, 5)]
    public string objectiveShort;
    public Sprite previewSprite;
    public string sceneName;

    [Header("HUD — цели миссии")]
    [Tooltip("Тексты для MissionQuestUI на сцене (назначь тот же asset в компоненте панели).")]
    public MissionObjectivesConfig objectivesConfig;
}