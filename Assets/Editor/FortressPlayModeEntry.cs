using UnityEditor;
using UnityEditor.SceneManagement;

// Keep the normal Play button consistent even when a level is open for editing.
[InitializeOnLoad]
public static class FortressPlayModeEntry
{
    public const string MenuPath = "Assets/Scenes/MainMenu.unity";

    static FortressPlayModeEntry()
    {
        EditorApplication.delayCall += Configure;
        EditorApplication.playModeStateChanged += StateChanged;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
            Configure();
    }

    public static void Configure()
    {
        if (EditorApplication.isPlaying) return;
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
        if (menu && EditorSceneManager.playModeStartScene != menu)
            EditorSceneManager.playModeStartScene = menu;
    }
}
