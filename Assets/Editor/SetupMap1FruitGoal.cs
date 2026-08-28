using System;
using System.IO;
using System.Linq;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupMap1FruitGoal
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        string request=Path.Combine(Work,"fruit-goal-request.txt");
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try { Setup(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"fruit-goal-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Configure Fruit Collection Goal")]
    public static void Setup()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode.");
        if(scene.isDirty)throw new InvalidOperationException("Save first, then use Tools > Map1 > Configure Fruit Collection Goal.");
        var roots=scene.GetRootGameObjects();
        var fruits=roots.SelectMany(r=>r.GetComponentsInChildren<AutoFruitPickup>(true)).ToArray();
        if(fruits.Length==0||fruits.Any(f=>!f.enabled||!f.gameObject.activeInHierarchy))throw new InvalidOperationException("No fruits or disabled fruits found; review fruit visibility first.");
        var goals=roots.SelectMany(r=>r.GetComponentsInChildren<FruitCollectionGoal>(true)).ToArray();
        if(goals.Length>1)throw new InvalidOperationException("Duplicate collection goal managers.");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-fruit-goal-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure collect all fruit goal");bool saved=false;
        try
        {
            FruitCollectionGoal goal;
            if(goals.Length==0)
            {
                var go=new GameObject("Map1 - Fruit Collection Goal");Undo.RegisterCreatedObjectUndo(go,"Add collection goal");goal=Undo.AddComponent<FruitCollectionGoal>(go);
            }
            else goal=goals[0];
            Undo.RecordObject(goal,"Register scene fruits");goal.Configure(fruits);EditorUtility.SetDirty(goal);
            var data=new SerializedObject(goal);
            if(data.FindProperty("fruits").arraySize!=fruits.Length)throw new InvalidOperationException("Fruit registration failed.");
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"fruit-goal-ready.txt"),"Registered "+fruits.Length+" scene fruits for collect-all goal. Next scene path: '"+data.FindProperty("nextScenePath").stringValue+"'. Target scene and Build Settings still require configuration. Play-mode tests pending. "+DateTime.Now);
            Debug.Log("Map1: registered "+fruits.Length+" fruits for collection goal. Configure the next scene to enable automatic loading.");
        }
        catch { if(!saved)Undo.RevertAllDownToGroup(undo);throw; }
    }
}
