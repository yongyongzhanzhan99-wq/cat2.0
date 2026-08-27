using System;
using System.IO;
using System.Linq;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupMap1FruitPickup
{
    const string Work = @"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath = "Assets/Scenes/Gamemap1.unity";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        var marker=Path.Combine(Work,"fruit-pickup-request.txt");
        if(!File.Exists(marker) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(marker);
        try { Setup(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"fruit-pickup-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Enable Automatic Fruit Pickup")]
    public static void Setup()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open Gamemap1 in Edit mode first.");
        if(scene.isDirty) throw new InvalidOperationException("Save the scene first, then use Tools > Map1 > Enable Automatic Fruit Pickup.");
        var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var group=transforms.SingleOrDefault(t=>t.name=="Map1 - Pathside Fruits");
        var player=transforms.Select(t=>t.GetComponent<CubeFirstPersonController>()).SingleOrDefault(p=>p!=null);
        if(group==null || player==null || group.childCount==0) throw new InvalidOperationException("Map1 fruit group or unique cube player not found.");
        var fruits=group.Cast<Transform>().ToArray();
        if(fruits.Any(t=>t.GetComponentsInChildren<Renderer>(true).Length==0)) throw new InvalidOperationException("Fruit group has a child without a visible model; aborting.");
        if(!AutoFruitPickup.IsWithinRadius(new Vector3(1.5f,0,0),1.5f)
           || AutoFruitPickup.IsWithinRadius(new Vector3(1.501f,0,0),1.5f)
           || !AutoFruitPickup.IsWithinRadius(new Vector3(0,0,1.49f),1.5f)
           || AutoFruitPickup.IsWithinRadius(new Vector3(1.1f,0,1.1f),1.5f)
           || AutoFruitPickup.IsWithinRadius(new Vector3(0,1.501f,0),1.5f))
            throw new InvalidOperationException("Pickup radius boundary tests failed.");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-fruit-pickup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Enable 1.5m automatic fruit pickup");
        bool saved=false;
        try
        {
            foreach(var fruit in fruits)
            {
                var renderers=fruit.GetComponentsInChildren<Renderer>(true);
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                var pickup=fruit.GetComponent<AutoFruitPickup>();
                if(pickup==null)pickup=Undo.AddComponent<AutoFruitPickup>(fruit.gameObject);
                Undo.RecordObject(pickup,"Configure fruit pickup");pickup.Configure(player.transform,bounds.center,1.5f);
                EditorUtility.SetDirty(pickup);PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
                var serialized=new SerializedObject(pickup);
                if(serialized.FindProperty("player").objectReferenceValue!=player.transform || serialized.FindProperty("pickupRadius").floatValue!=1.5f)
                    throw new InvalidOperationException("Fruit configuration verification failed.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"fruit-pickup-ready.txt"),"Saved "+fruits.Length+" automatic fruit pickups. Radius=1.5; player references and 3D distance boundary tests passed. Play-mode approach test still required. "+DateTime.Now);
            Debug.Log("Map1: automatic fruit pickup saved for "+fruits.Length+" fruits (radius 1.5).");
        }
        catch { if(!saved)Undo.RevertAllDownToGroup(undo);throw; }
    }
}
