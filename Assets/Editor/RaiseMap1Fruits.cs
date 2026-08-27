using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RaiseMap1Fruits
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        string marker=Path.Combine(Work,"raise-fruits-request.txt");
        if(!File.Exists(marker) || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(marker);
        try { Raise(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"raise-fruits-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Raise Fruits For Jump Pickup")]
    public static void Raise()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode first.");
        if(scene.isDirty)throw new InvalidOperationException("Save the scene, then use Tools > Map1 > Raise Fruits For Jump Pickup.");
        var roots=scene.GetRootGameObjects();
        var group=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Map1 - Pathside Fruits");
        var player=roots.SelectMany(r=>r.GetComponentsInChildren<CubeFirstPersonController>()).Single();
        var playerData=new SerializedObject(player);
        float jumpHeight=playerData.FindProperty("jumpHeight").floatValue;
        float gravity=playerData.FindProperty("gravity").floatValue;
        if(gravity>=0 || jumpHeight<=0)throw new InvalidOperationException("Invalid player jump settings.");
        // Match controller integration at its largest timestep, to allow low framerates.
        float velocity=Mathf.Sqrt(2*Mathf.Abs(gravity)*jumpHeight),height=0,apex=0;
        for(int i=0;i<200;i++) { velocity+=gravity*.05f;height+=velocity*.05f;apex=Mathf.Max(apex,height);if(velocity<0)break; }
        var ground=roots.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled && !c.isTrigger && c.name.Contains("terrain_")).ToArray();
        Physics.SyncTransforms();
        var planned=new Dictionary<Transform,Vector3>();
        var report=new List<string>();
        foreach(Transform fruit in group)
        {
            var pickup=fruit.GetComponent<AutoFruitPickup>();
            if(pickup==null)throw new InvalidOperationException("Automatic pickup missing on "+fruit.name);
            var data=new SerializedObject(pickup);
            float radius=data.FindProperty("pickupRadius").floatValue;
            if(Mathf.Abs(radius-1.5f)>.001f || data.FindProperty("player").objectReferenceValue!=player.transform)throw new InvalidOperationException("Unexpected pickup configuration.");
            var center=fruit.TransformPoint(data.FindProperty("localPickupPoint").vector3Value);
            float baseY=GroundY(ground,center);
            if(float.IsNegativeInfinity(baseY))throw new InvalidOperationException("No ground under "+fruit.name);
            float targetY=baseY+radius+.5f;
            // Account for nearby terrain slopes, not just ground directly below the fruit.
            var samples=new List<Vector3>();
            for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
            {
                float dx=x*.25f,dz=z*.25f,d2=dx*dx+dz*dz;if(d2>radius*radius)continue;
                var p=center+new Vector3(dx,0,dz);float y=GroundY(ground,p);if(float.IsNegativeInfinity(y))continue;
                p.y=y+.06f;samples.Add(p);
                targetY=Mathf.Max(targetY,p.y+Mathf.Sqrt(radius*radius-d2)+.2f);
            }
            if(targetY-(baseY+apex)>radius-.15f)throw new InvalidOperationException("Terrain too steep for a reliable jump pickup at "+fruit.name+"; no positions changed.");
            var desired=new Vector3(center.x,targetY,center.z);
            if(samples.Any(p=>AutoFruitPickup.IsWithinRadius(desired-p,radius)))throw new InvalidOperationException("Standing pickup still possible at "+fruit.name);
            if(!AutoFruitPickup.IsWithinRadius(desired-new Vector3(center.x,baseY+apex,center.z),radius))throw new InvalidOperationException("Jump cannot reach "+fruit.name);
            planned.Add(fruit,fruit.position+Vector3.up*(targetY-center.y));
            report.Add(fruit.name+": fruit center "+(targetY-baseY).ToString("F2")+" above ground; standing samples outside radius; simulated jump apex inside radius.");
        }
        if(planned.Count==0)throw new InvalidOperationException("No fruits found.");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-raised-fruits-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Raise fruits for jumping");
        bool saved=false;
        try
        {
            foreach(var p in planned) { Undo.RecordObject(p.Key,"Raise fruit");p.Key.position=p.Value;PrefabUtility.RecordPrefabInstancePropertyModifications(p.Key); }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            saved=true;Undo.CollapseUndoOperations(undo);
            report.Insert(0,"Saved "+planned.Count+" raised fruits; pickup radius unchanged at 1.5. "+DateTime.Now);
            report.Add("Geometry and jump-integration checks only; Play-mode walkthrough still required.");
            File.WriteAllLines(Path.Combine(Work,"raise-fruits-ready.txt"),report);Debug.Log(string.Join("\n",report));
        }
        catch { if(!saved)Undo.RevertAllDownToGroup(undo);throw; }
    }
    static float GroundY(MeshCollider[] ground,Vector3 p)
    {
        float highest=float.NegativeInfinity;
        foreach(var c in ground)if(c.Raycast(new Ray(p+Vector3.up*30,Vector3.down),out var hit,100)&&hit.normal.y>.65f)highest=Mathf.Max(highest,hit.point.y);
        return highest;
    }
}
