using System;
using System.IO;
using System.Linq;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MigrateMap1Rigidbody
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        string p=Path.Combine(Work,"rigidbody-migration-request.txt");
        if(!File.Exists(p)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(p);
        try { Setup(); }catch(Exception e){File.WriteAllText(Path.Combine(Work,"rigidbody-migration-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Map1/Migrate To Rigidbody Player")]
    public static void Setup()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode.");
        if(scene.isDirty)throw new InvalidOperationException("Save first, then use Tools > Map1 > Migrate To Rigidbody Player.");
        var roots=scene.GetRootGameObjects();
        var player=roots.SelectMany(r=>r.GetComponentsInChildren<CubeFirstPersonController>(true)).Single();
        var oldController=player.GetComponent<CharacterController>();
        if(!player.gameObject.activeInHierarchy)throw new InvalidOperationException("Player hierarchy is disabled.");
        var fruits=roots.SelectMany(r=>r.GetComponentsInChildren<AutoFruitPickup>(true)).ToArray();
        if(fruits.Length==0)throw new InvalidOperationException("No fruit pickups found.");
        var solidRoots=new[]{"Terrain","Path","Buildings","Props","Exterior","Rocks"};
        var meshes=roots.Where(r=>solidRoots.Contains(r.name)).SelectMany(r=>r.GetComponentsInChildren<MeshFilter>())
            .Concat(roots.Where(r=>r.name=="Vegetation").SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Where(f=>f.name.Contains("_tree_")))
            .Where(f=>f.sharedMesh!=null&&f.GetComponentInParent<AutoFruitPickup>()==null).Distinct().ToArray();
        if(meshes.Any(f=>f.GetComponentInParent<Rigidbody>()!=null))throw new InvalidOperationException("Dynamic scenery found; refusing to replace its physics.");
        var data=new SerializedObject(player);float gravity=data.FindProperty("gravity").floatValue,jump=data.FindProperty("jumpHeight").floatValue;
        if(gravity>=0||jump<=0)throw new InvalidOperationException("Player gravity/jump settings invalid.");
        float v=Mathf.Sqrt(2*Mathf.Abs(gravity)*jump),h=0,apex=0;
        for(int i=0;i<200;i++){v+=gravity*.05f;h+=v*.05f;apex=Mathf.Max(apex,h);if(v<0)break;}
        float centerHeight=.7f;
        if((player.transform.lossyScale-Vector3.one).sqrMagnitude>.0001f)throw new InvalidOperationException("Player scale must be one.");
        if(2-centerHeight-.06f<=.75f||Mathf.Abs(2-centerHeight-apex)>.75f)throw new InvalidOperationException("Player cannot support the requested jump pickup height.");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-jump-physics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Set jump pickup and scene collisions");bool saved=false;
        try
        {
            int added=0;
            foreach(var mesh in meshes)
            {
                var colliders=mesh.GetComponents<Collider>();
                if(colliders.Length==0){var c=Undo.AddComponent<MeshCollider>(mesh.gameObject);c.sharedMesh=mesh.sharedMesh;c.convex=false;added++;}
                else foreach(var c in colliders)
                {
                    // Leave intentionally disabled legacy colliders alone when a working replacement exists.
                    if(!c.enabled&&colliders.Any(other=>other.enabled&&!other.isTrigger))continue;
                    Undo.RecordObject(c,"Enable scenery collision");c.enabled=true;c.isTrigger=false;
                    if(c is MeshCollider mc){mc.sharedMesh=mesh.sharedMesh;mc.convex=false;}
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                }
            }
            if(oldController!=null)Undo.DestroyObjectImmediate(oldController);
            var body=player.GetComponent<Rigidbody>();if(body==null)body=Undo.AddComponent<Rigidbody>(player.gameObject);
            Undo.RecordObject(body,"Configure rigidbody");body.isKinematic=false;body.useGravity=true;body.detectCollisions=true;body.mass=1;body.drag=0;body.angularDrag=.05f;
            body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations=12;body.solverVelocityIterations=8;
            var box=player.GetComponent<BoxCollider>();if(box==null)box=Undo.AddComponent<BoxCollider>(player.gameObject);
            Undo.RecordObject(box,"Configure box collision");box.center=new Vector3(0,.7f,0);box.size=new Vector3(1.2f,1.4f,1.2f);box.enabled=true;box.isTrigger=false;
            foreach(var childCollider in player.GetComponentsInChildren<Collider>(true))if(childCollider!=box)throw new InvalidOperationException("Unexpected extra player collider: "+childCollider.name);
            Undo.RecordObject(player,"Enable player controller");player.enabled=true;
            var ground=roots.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&!c.isTrigger&&c.name.Contains("terrain_")).ToArray();
            Physics.SyncTransforms();float spawnY=Surface(ground,player.transform.position);
            if(float.IsNegativeInfinity(spawnY))throw new InvalidOperationException("No collision ground under player spawn.");
            Undo.RecordObject(player.transform,"Place player on ground");var spawn=player.transform.position;spawn.y=spawnY+.06f;player.transform.position=spawn;
            if(ground.Any(g=>Physics.GetIgnoreLayerCollision(player.gameObject.layer,g.gameObject.layer)))throw new InvalidOperationException("Player/ground layer collision is disabled.");
            foreach(var fruit in fruits)
            {
                var fruitData=new SerializedObject(fruit);var center=fruit.transform.TransformPoint(fruitData.FindProperty("localPickupPoint").vector3Value);
                Transform interior=fruit.transform.parent;
                while(interior!=null&&interior.name!="Map1 - Interior")interior=interior.parent;
                Collider[] support=interior!=null ? interior.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Timber floor").Cast<Collider>().ToArray() : ground.Cast<Collider>().ToArray();
                float y=Surface(support,center);
                if(float.IsNegativeInfinity(y))throw new InvalidOperationException("No floor below fruit: "+fruit.name);
                var desired=new Vector3(center.x,y+2,center.z);
                Undo.RecordObject(fruit.transform,"Set fruit height 2");fruit.transform.position+=desired-center;
                Undo.RecordObject(fruit,"Set pickup diameter 1.5");fruit.enabled=true;fruit.Configure(player.transform,desired,.75f);
                if(!fruit.gameObject.activeSelf){Undo.RecordObject(fruit.gameObject,"Restore fruit visibility");fruit.gameObject.SetActive(true);}
                PrefabUtility.RecordPrefabInstancePropertyModifications(fruit.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(fruit);
            }
            Physics.SyncTransforms();
            if(player.GetComponent<CharacterController>()!=null||!body.useGravity||body.isKinematic||!box.enabled)throw new InvalidOperationException("Rigidbody migration verification failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"rigidbody-migration-ready.txt"),"Saved "+fruits.Length+" fruits: diameter 1.5 / radius 0.75, center 2 units above own ground/floor; distance from player body center.\nAdded "+added+" static mesh colliders; inspected "+meshes.Length+" solid meshes. Rigidbody + BoxCollider enabled; CharacterController removed, spawn supported, ground layers checked.\nStanding reach excluded and conservative jump reach checked. Play-mode interaction still needs testing. "+DateTime.Now);
            Debug.Log("Map1 jump pickup diameter 1.5 and scene collisions configured.");
        }
        catch {if(!saved)Undo.RevertAllDownToGroup(undo);throw;}
    }
    static float Surface(System.Collections.Generic.IEnumerable<Collider> colliders,Vector3 p)
    {
        float y=float.NegativeInfinity;foreach(var c in colliders)if(c.enabled&&c.Raycast(new Ray(p+Vector3.up*30,Vector3.down),out var hit,100)&&hit.normal.y>.65f)y=Mathf.Max(y,hit.point.y);return y;
    }
}
