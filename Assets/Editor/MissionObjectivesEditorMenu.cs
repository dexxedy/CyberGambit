#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MissionObjectivesEditorMenu
{
    [MenuItem("CyberGambit/Create Mission Quest Config")]
    public static void CreateQuestConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Mission Quest Config",
            "MissionQuest",
            "asset",
            "Выберите путь для конфига целей миссии");

        if (string.IsNullOrEmpty(path))
            return;

        var asset = ScriptableObject.CreateInstance<MissionObjectivesConfig>();
        asset.panelTitle = "Задачи миссии";
        asset.objectiveTexts = new[] { "Первая цель", "Вторая цель" };
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
#endif
