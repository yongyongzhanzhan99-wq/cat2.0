using System;
using System.IO;
using System.Linq;
using CatGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupMap1CubePlayer
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        var marker=Path.Combine(Work,"cube-player-request.txt");
        if(!File.Exists(marker) || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=ScenePath) return;
        File.Delete(marker);
        try { Setup(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"cube-player-error.txt"),e.ToString());Debug.LogException(e); }
    }

    [MenuItem("Tools/Map1/Add First Person Cube Player")]
    public static void Setup()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open Gamemap1 in Edit mode first.");
        if(scene.isDirty) throw new InvalidOperationException("Save your existing scene edits first, then use Tools > Map1 > Add First Person Cube Player.");
        if(scene.GetRootGameObjects().Any(g=>g.GetComponentInChildren<CubeFirstPersonController>(true)!=null)) throw new InvalidOperationException("The cube player already exists; refusing duplicates.");
        var cameras=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
        var camera=cameras.FirstOrDefault(c=>c.name=="Main camera") ?? cameras.FirstOrDefault(c=>c.CompareTag("MainCamera"));
        if(camera==null) throw new InvalidOperationException("Map1 Main Camera not found.");
        var spawnXZ=camera.transform.position;
        Physics.SyncTransforms();
        var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled && !c.isTrigger && c.name.Contains("terrain_")).ToArray();
        float groundY=float.NegativeInfinity;
        foreach(var collider in ground)
        {
            RaycastHit hit;
            if(collider.Raycast(new Ray(new Vector3(spawnXZ.x,spawnXZ.y+20,spawnXZ.z),Vector3.down),out hit,100) && hit.normal.y>.7f)
                groundY=Mathf.Max(groundY,hit.point.y);
        }
        if(float.IsNegativeInfinity(groundY))throw new InvalidOperationException("No walkable collision surface below the entry camera.");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-cube-player.unity"),true);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add first person cube player");
        try
        {
            var player=new GameObject("Map1 - Cube Player");Undo.RegisterCreatedObjectUndo(player,"Create cube player");
            player.transform.SetPositionAndRotation(new Vector3(spawnXZ.x,groundY+.06f,spawnXZ.z),Quaternion.Euler(0,camera.transform.eulerAngles.y,0));
            var controller=Undo.AddComponent<CharacterController>(player);
            controller.height=1.4f;controller.radius=.6f;controller.center=new Vector3(0,.7f,0);
            controller.stepOffset=.25f;controller.slopeLimit=45;controller.skinWidth=.04f;controller.minMoveDistance=0;
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(body,"Create cube body");
            body.name="Cube Body";body.transform.SetParent(player.transform,false);body.transform.localPosition=new Vector3(0,.7f,0);body.transform.localScale=Vector3.one*1.4f;
            Undo.DestroyObjectImmediate(body.GetComponent<BoxCollider>());
            Undo.SetTransformParent(camera.transform,player.transform,"Attach first person camera");Undo.RecordObject(camera.transform,"Set eye position");Undo.RecordObject(camera,"Set first person lens");
            camera.transform.localPosition=new Vector3(0,1.2f,0);camera.transform.localRotation=Quaternion.identity;camera.transform.localScale=Vector3.one;
            camera.orthographic=false;camera.fieldOfView=70;camera.nearClipPlane=.05f;
            Undo.RecordObject(camera.gameObject,"Set main camera tag");camera.gameObject.tag="MainCamera";
            camera.gameObject.SetActive(true);
            foreach(var c in cameras) { Undo.RecordObject(c,"Select player camera");c.enabled=c==camera; }
            var listeners=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AudioListener>(true)).ToArray();
            foreach(var l in listeners) { Undo.RecordObject(l,"Select player audio listener");l.enabled=l.gameObject==camera.gameObject; }
            if(camera.GetComponent<AudioListener>()==null)Undo.AddComponent<AudioListener>(camera.gameObject);
            var movement=Undo.AddComponent<CubeFirstPersonController>(player);movement.Configure(camera,body.GetComponent<Renderer>());EditorUtility.SetDirty(movement);
            if(camera.transform.parent!=player.transform || player.GetComponent<Rigidbody>()!=null || body.GetComponent<Collider>()!=null)
                throw new InvalidOperationException("Unexpected player component configuration.");
            if(cameras.Count(c=>c.enabled)!=1)throw new InvalidOperationException("Expected exactly one enabled scene camera.");
            Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Failed to save Map1.");
            File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.with-cube-player.unity"),true);
            Selection.activeGameObject=player;
            File.WriteAllText(Path.Combine(Work,"cube-player-ready.txt"),"Saved first-person cube player in Gamemap1. Spawn="+player.transform.position+"; camera parent, collider, single camera checked. Runtime movement not yet play-tested. "+DateTime.Now);
            Debug.Log("MAP1_CUBE_PLAYER_READY");
        }
        catch { Undo.RevertAllDownToGroup(group);throw; }
    }
}
