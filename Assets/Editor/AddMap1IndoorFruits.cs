using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AddMap1IndoorFruits
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    const string ItemName="Map1 - Indoor Fruit";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        var request=Path.Combine(Work,"indoor-fruits-request.txt");
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try { Add(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"indoor-fruits-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Add Indoor Fruits")]
    public static void Add()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode first.");
        if(scene.isDirty)throw new InvalidOperationException("Save first, then use Tools > Map1 > Add Indoor Fruits.");
        var roots=scene.GetRootGameObjects();
        var houses=roots.Single(r=>r.name=="Buildings").transform.Cast<Transform>().ToArray();
        var sources=roots.Single(r=>r.name=="Map1 - Pathside Fruits").GetComponentsInChildren<AutoFruitPickup>();
        var player=roots.SelectMany(r=>r.GetComponentsInChildren<CubeFirstPersonController>()).Single();
        var controller=player.GetComponent<CharacterController>();
        var playerData=new SerializedObject(player);float jump=playerData.FindProperty("jumpHeight").floatValue;
        var ground=roots.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&!c.isTrigger&&c.name.Contains("terrain_")).ToArray();
        if(houses.Length==0||sources.Length<3||controller==null)throw new InvalidOperationException("Expected houses, outdoor fruit templates and player not found.");
        Physics.SyncTransforms();
        var centers=new List<Vector3>();var selected=new List<AutoFruitPickup>();var parents=new List<Transform>();
        for(int i=0;i<houses.Length;i++)
        {
            var house=houses[i];var interior=house.Find("Map1 - Interior");
            if(interior==null||interior.Find(ItemName)!=null)throw new InvalidOperationException("Missing interior or indoor fruit already exists: "+house.name);
            var floor=interior.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Timber floor").OrderByDescending(c=>c.bounds.size.x*c.bounds.size.z).First();
            var feet=floor.transform.TransformPoint(floor.center+Vector3.up*floor.size.y*.5f);
            var source=sources[i%3];var data=new SerializedObject(source);
            float radius=data.FindProperty("pickupRadius").floatValue;
            var sourceCenter=source.transform.TransformPoint(data.FindProperty("localPickupPoint").vector3Value);
            float outdoorGround=float.NegativeInfinity;
            foreach(var c in ground)if(c.Raycast(new Ray(sourceCenter+Vector3.up*30,Vector3.down),out var hit,100)&&hit.normal.y>.65f)outdoorGround=Mathf.Max(outdoorGround,hit.point.y);
            float height=sourceCenter.y-outdoorGround;
            if(float.IsNegativeInfinity(outdoorGround)||Mathf.Abs(radius-1.5f)>.001f||height<=radius+.15f||height>=radius+jump-.2f)
                throw new InvalidOperationException("Outdoor template has unexpected jump pickup settings.");
            var center=feet+Vector3.up*height;
            // Ensure both standing and jumping fit under the roof and between the walls.
            var shell=house.GetComponents<MeshCollider>().Single(c=>c.enabled);
            float r=controller.radius+.04f;
            foreach(float rise in new[]{.06f,jump+.06f})
            {
                var basePoint=feet+Vector3.up*rise;
                var overlaps=Physics.OverlapCapsule(basePoint+Vector3.up*r,basePoint+Vector3.up*(controller.height-r),r,~0,QueryTriggerInteraction.Ignore);
                if(overlaps.Contains(shell))throw new InvalidOperationException("Insufficient jump clearance in "+house.name);
            }
            if(shell.Raycast(new Ray(feet+Vector3.up*.1f,Vector3.up),out var roof,10)&&roof.distance<Mathf.Max(height+.5f,controller.height+jump+.2f))
                throw new InvalidOperationException("Roof is too low for this fruit: "+house.name);
            if(AutoFruitPickup.IsWithinRadius(center-(feet+Vector3.up*.06f),radius)||!AutoFruitPickup.IsWithinRadius(center-(feet+Vector3.up*(jump*.8f)),radius))
                throw new InvalidOperationException("Jump pickup reach check failed.");
            centers.Add(center);selected.Add(source);parents.Add(interior);
        }
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-indoor-fruits-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add fruits inside houses");bool saved=false;
        try
        {
            for(int i=0;i<houses.Length;i++)
            {
                var source=selected[i];var copy=UnityEngine.Object.Instantiate(source.gameObject,parents[i],true);
                Undo.RegisterCreatedObjectUndo(copy,"Add indoor fruit");copy.name=ItemName;
                var pickup=copy.GetComponent<AutoFruitPickup>();var d=new SerializedObject(pickup);
                var center=copy.transform.TransformPoint(d.FindProperty("localPickupPoint").vector3Value);
                copy.transform.position+=centers[i]-center;
                // Cloning retains size, model, material, orientation and the same pickup script.
                var check=new SerializedObject(pickup);
                if(check.FindProperty("player").objectReferenceValue!=player.transform || check.FindProperty("pickupRadius").floatValue!=1.5f || (copy.transform.lossyScale-source.transform.lossyScale).sqrMagnitude>.00001f)
                    throw new InvalidOperationException("Copied fruit parameters differ from outdoor fruit.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"indoor-fruits-ready.txt"),"Saved "+houses.Length+" indoor fruits, one per house. "+DateTime.Now+"\nCopied outdoor model sizes, materials, radius=1.5, player reference and AutoFruitPickup. Height above indoor floor matches outdoor height above terrain. Standing/jumping reach and shell clearance checks passed.\nOutdoor fruits unchanged. Play-mode pickup test still required.");
            Debug.Log("Map1: added "+houses.Length+" indoor fruits using outdoor pickup parameters.");
        }
        catch { if(!saved)Undo.RevertAllDownToGroup(undo);throw; }
    }
}
