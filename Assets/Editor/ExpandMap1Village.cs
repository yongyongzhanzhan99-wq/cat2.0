using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CatGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExpandMap1Village
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath="Assets/Scenes/Gamemap1.unity";
    const string Marker="Map1 - Village Expanded 1.5x";
    const string AssetFolder="Assets/Map1_VillageExpansion";
    const float Factor=1.5f;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall+=Requested; }
    static void Requested()
    {
        var request=Path.Combine(Work,"expand-village-request.txt");
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try { Expand(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"expand-village-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    static Vector3 Spread(Vector3 p,Vector3 pivot) { return new Vector3(pivot.x+(p.x-pivot.x)*Factor,p.y,pivot.z+(p.z-pivot.z)*Factor); }
    static void Position(Transform t,Vector3 p) { Undo.RecordObject(t,"Move village object");t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t); }
    [MenuItem("Tools/Map1/Expand Village 1.5x")]
    public static void Expand()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open Gamemap1 in Edit mode.");
        if(scene.isDirty)throw new InvalidOperationException("Save first, then use Tools > Map1 > Expand Village 1.5x.");
        var roots=scene.GetRootGameObjects();
        if(roots.Any(g=>g.name==Marker))throw new InvalidOperationException("Village is already expanded. Refusing a second multiplication.");
        Func<string,Transform> root=name=>roots.Single(g=>g.name==name).transform;
        var player=roots.SelectMany(g=>g.GetComponentsInChildren<CubeFirstPersonController>()).Single();
        var pivot=player.transform.position; // Preserve the tested spawn point.
        var path=root("Path");var terrain=root("Terrain");var exterior=root("Exterior");
        var stretch=new[]{terrain,path,exterior};
        foreach(var t in stretch)if(Quaternion.Angle(t.rotation,Quaternion.identity)>.01f)throw new InvalidOperationException("Unexpected rotated layout root: "+t.name);
        var buildings=root("Buildings").Cast<Transform>().ToArray();
        var fruits=root("Map1 - Pathside Fruits").GetComponentsInChildren<AutoFruitPickup>(true);
        if(fruits.Length==0)throw new InvalidOperationException("No configured fruits found.");
        var movable=new HashSet<Transform>(buildings);
        // Stop at a whole prefab/object so its component pieces keep their arrangement.
        foreach(string name in new[]{"Props","Vegetation","Rocks","Sky"})Collect(root(name),movable);
        var prompt=root("Map1 - Floating Entrance Prompt");movable.Add(prompt);
        var moves=movable.ToDictionary(t=>t,t=>Spread(t.position,pivot));
        var fruitMoves=new Dictionary<Transform,Vector3>();
        foreach(var f in fruits)
        {
            var data=new SerializedObject(f);var local=data.FindProperty("localPickupPoint").vector3Value;
            var center=f.transform.TransformPoint(local);
            fruitMoves.Add(f.transform,f.transform.position+Spread(center,pivot)-center);
        }
        var oldPath=BoundsOf(path);var oldTerrain=BoundsOf(terrain);
        var ground=roots.SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&!c.isTrigger&&c.name.Contains("terrain_")).ToArray();
        if(ground.Length==0)throw new InvalidOperationException("No ground colliders.");
        var playerData=new SerializedObject(player);
        float speed=playerData.FindProperty("walkSpeed").floatValue,jump=playerData.FindProperty("jumpHeight").floatValue;
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-expansion-"+stamp+".unity"));
        if(!AssetDatabase.IsValidFolder(AssetFolder))AssetDatabase.CreateFolder("Assets","Map1_VillageExpansion");
        string runFolder=AssetFolder+"/Expansion_"+stamp;AssetDatabase.CreateFolder(AssetFolder,"Expansion_"+stamp);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Expand village 1.5x");bool saved=false;
        try
        {
            foreach(var t in stretch)
            {
                Position(t,Spread(t.position,pivot));Undo.RecordObject(t,"Extend connected ground and paths");
                t.localScale=Vector3.Scale(t.localScale,new Vector3(Factor,1,Factor));PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
            foreach(var m in moves)Position(m.Key,m.Value);
            foreach(var m in fruitMoves)Position(m.Key,m.Value);
            Physics.SyncTransforms();
            int ramps=0;
            foreach(var house in buildings)
            {
                var interior=house.Find("Map1 - Interior");
                if(interior==null)throw new InvalidOperationException("Enterable interior missing: "+house.name);
                var floors=interior.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Timber floor").ToArray();
                float lift=0;
                foreach(var floor in floors)
                {
                    // Inspect the actual rectangular floor, including near its edges.
                    for(int x=0;x<3;x++)for(int z=0;z<3;z++)
                    {
                        var sample=floor.transform.TransformPoint(floor.center+Vector3.Scale(floor.size,new Vector3((x-1)*.4f,.5f,(z-1)*.4f)));
                        float g=GroundY(ground,sample);if(!float.IsNegativeInfinity(g))lift=Mathf.Max(lift,g+.025f-sample.y);
                    }
                }
                if(lift>.5f)throw new InvalidOperationException("House needs manual terrain adjustment: "+house.name);
                if(lift>0)Position(house,house.position+Vector3.up*lift);
                var ramp=interior.Find("Entrance ramp");
                if(ramp==null)throw new InvalidOperationException("Entrance ramp missing.");
                var filter=ramp.GetComponent<MeshFilter>();var collider=ramp.GetComponent<MeshCollider>();
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="RegroundedRamp_"+house.GetInstanceID();
                var vertices=mesh.vertices;
                if(vertices.Length!=8)throw new InvalidOperationException("Unexpected entrance mesh.");
                var end=ramp.TransformPoint((vertices[2]+vertices[3])*.5f);float groundY=GroundY(ground,end);
                if(float.IsNegativeInfinity(groundY))throw new InvalidOperationException("No terrain at ramp end: "+house.name);
                float low=ramp.InverseTransformPoint(new Vector3(end.x,groundY+.015f,end.z)).y;
                float length=Mathf.Abs(vertices[2].z-vertices[0].z);
                if(Mathf.Abs(low-vertices[0].y)>length*.65f)throw new InvalidOperationException("Ramp too steep: "+house.name);
                vertices[2].y=vertices[3].y=low;vertices[6].y=vertices[7].y=low-.18f;
                mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh,runFolder+"/"+mesh.name+".asset");
                Undo.RecordObject(filter,"Adjust entrance ramp");Undo.RecordObject(collider,"Adjust ramp collision");filter.sharedMesh=mesh;collider.sharedMesh=mesh;ramps++;
            }
            Physics.SyncTransforms();
            var newPath=BoundsOf(path);var newTerrain=BoundsOf(terrain);
            if(Mathf.Abs(newPath.size.x-oldPath.size.x*Factor)>.05f || Mathf.Abs(newPath.size.z-oldPath.size.z*Factor)>.05f)
                throw new InvalidOperationException("Path extent verification failed.");
            float spawnGround=GroundY(ground,pivot);
            if(float.IsNegativeInfinity(spawnGround)||Mathf.Abs(spawnGround-pivot.y)>.3f)throw new InvalidOperationException("Spawn ground support changed unexpectedly.");
            foreach(var f in fruits)
            {
                var d=new SerializedObject(f);float radius=d.FindProperty("pickupRadius").floatValue;
                var center=f.transform.TransformPoint(d.FindProperty("localPickupPoint").vector3Value);float g=GroundY(ground,center);
                float h=center.y-g;
                if(float.IsNegativeInfinity(g)||Mathf.Abs(radius-1.5f)>.001f||h<=radius+.15f||h>=radius+jump-.2f)
                    throw new InvalidOperationException("Jump pickup clearance changed: "+f.name);
            }
            var marker=new GameObject(Marker);Undo.RegisterCreatedObjectUndo(marker,"Mark expanded layout");
            marker.transform.position=pivot;
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save expanded scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"expand-village-ready.txt"),"Saved village expansion 1.5x XZ. "+DateTime.Now+"\nPath bounds: "+oldPath.size+" -> "+newPath.size+"\nTerrain bounds: "+oldTerrain.size+" -> "+newTerrain.size+"\nMoved "+buildings.Length+" houses and "+fruits.Length+" fruits, regrounded "+ramps+" ramps. Spawn ground, path size, and jump pickup heights checked.\nPlayer speed="+speed+", jump height="+jump+", pickup radius=1.5 unchanged. House/fruit/tree model sizes unchanged.\nPlay-mode walkthrough not yet performed.");
            Debug.Log("Map1 village expanded 1.5x; paths, spawn ground, ramps and fruit heights checked.");
        }
        catch { if(!saved){Undo.RevertAllDownToGroup(undo);AssetDatabase.DeleteAsset(runFolder);}throw; }
    }
    static void Collect(Transform group,HashSet<Transform> output)
    {
        foreach(Transform child in group)
        {
            if(PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)||child.GetComponent<Renderer>()!=null)output.Add(child);
            else Collect(child,output);
        }
    }
    static Bounds BoundsOf(Transform t)
    {
        var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new InvalidOperationException("No geometry: "+t.name);
        var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static float GroundY(MeshCollider[] ground,Vector3 p)
    {
        float y=float.NegativeInfinity;foreach(var c in ground)if(c.Raycast(new Ray(p+Vector3.up*40,Vector3.down),out var hit,100)&&hit.normal.y>.65f)y=Mathf.Max(y,hit.point.y);return y;
    }
}
