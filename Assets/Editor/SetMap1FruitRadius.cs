using System;
using System.IO;
using System.Linq;
using CatGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetMap1FruitRadius
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        var marker=Path.Combine(Work,"fruit-radius-request.txt");
        if(!File.Exists(marker)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(marker);
        try { Apply(); }catch(Exception e){File.WriteAllText(Path.Combine(Work,"fruit-radius-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Map1/Set All Fruit Radius To 1.5")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gamemap1.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode.");
        if(scene.isDirty)throw new InvalidOperationException("Save the scene, then use Tools > Map1 > Set All Fruit Radius To 1.5.");
        var fruits=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AutoFruitPickup>(true)).ToArray();
        if(fruits.Length==0)throw new InvalidOperationException("No fruit pickups found.");
        Directory.CreateDirectory(Work);
        File.Copy(scene.path,Path.Combine(Work,"Gamemap1.before-radius-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Set all fruit pickup radii to 1.5");bool saved=false;
        try
        {
            foreach(var fruit in fruits)
            {
                var data=new SerializedObject(fruit);
                data.FindProperty("pickupRadius").floatValue=1.5f;
                data.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(fruit);
                if(new SerializedObject(fruit).FindProperty("pickupRadius").floatValue!=1.5f)throw new InvalidOperationException("Radius verification failed.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"fruit-radius-ready.txt"),"Saved "+fruits.Length+" fruit pickup radii = 1.5 (diameter 3). Positions and jump-only logic unchanged. "+DateTime.Now);
            Debug.Log("Map1: all "+fruits.Length+" fruit pickup radii saved as 1.5.");
        }
        catch {if(!saved)Undo.RevertAllDownToGroup(undo);throw;}
    }
}
