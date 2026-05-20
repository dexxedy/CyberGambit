using UnityEngine;

/// <summary>
/// Тексты целей миссии. Прогресс обновляется из кода через <see cref="MissionQuestUI"/>.
/// </summary>
[CreateAssetMenu(fileName = "MissionQuest", menuName = "CyberGambit/Mission Quest Config")]
public class MissionObjectivesConfig : ScriptableObject
{
    [Tooltip("Заголовок панели.")]
    public string panelTitle = "Задачи миссии";

    [Tooltip("Строки целей по порядку (индекс 0, 1, 2… для SetProgress / Complete).")]
    [TextArea(1, 3)]
    public string[] objectiveTexts;
}
