using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnsureMap2BuildScene
{
    private const string Map2ScenePath = "Assets/map2.unity";
    private const string LegacyMap2ScenePath = "Assets/Scenes/map2.unity";

    static EnsureMap2BuildScene()
    {
        EditorApplication.delayCall += EnsureRegistered;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                EnsureRegistered();
        };
    }

    private static void EnsureRegistered()
    {
        if (!File.Exists(Map2ScenePath))
        {
            Debug.LogError("Map2 scene is missing: " + Map2ScenePath);
            return;
        }

        var scenes = EditorBuildSettings.scenes.ToList();
        // Retain the legacy scene asset, but do not register two scenes named
        // "map2".  Name-based scene loading can otherwise open the wrong map.
        bool changed = scenes.RemoveAll(scene => scene.path == LegacyMap2ScenePath) > 0;
        int index = scenes.FindIndex(scene => scene.path == Map2ScenePath);
        changed |= index < 0 || !scenes[index].enabled;
        if (index >= 0)
            scenes[index] = new EditorBuildSettingsScene(Map2ScenePath, true);
        else
            scenes.Add(new EditorBuildSettingsScene(Map2ScenePath, true));

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Map2 Build Settings registration was repaired.");
        }
    }
}
