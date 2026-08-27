using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateMap1Ground
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    static readonly string[] Names={"rpgpp_lt_terrain_grass_01","rpgpp_lt_terrain_grass_02","rpgpp_lt_terrain_path_01a","rpgpp_lt_terrain_path_01b","rpgpp_lt_terrain_sand_01"};
    [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Map1/Verify Ground Collisions")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!="Assets/Scenes/Gamemap1.unity")return;
        try { Verify(); } catch(Exception e){File.WriteAllText(Path.Combine(Work,"ground-error.txt"),e.ToString());Debug.LogException(e);}
    }
    static void Verify()
    {
        var scene=SceneManager.GetActiveScene();
        var meshes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(m=>Names.Any(n=>m.name.StartsWith(n))).ToArray();
        if(meshes.Length==0)throw new Exception("No Map1 ground meshes found.");
        Physics.SyncTransforms();
        int hits=0;
        foreach(var mesh in meshes)
        {
            var col=mesh.GetComponent<MeshCollider>();
            if(col==null || !col.enabled || col.isTrigger || col.convex || col.sharedMesh!=mesh.sharedMesh || col.attachedRigidbody!=null)
                throw new Exception("Invalid static ground collider: "+mesh.name);
            var b=col.bounds; bool hitAny=false;
            for(int x=1;x<5;x++)for(int z=1;z<5;z++)
            {
                var start=new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/5f),b.max.y+2,Mathf.Lerp(b.min.z,b.max.z,z/5f));RaycastHit hit;
                if(col.Raycast(new Ray(start,Vector3.down),out hit,b.size.y+4) && hit.normal.y>.5f)
                {hitAny=true;}
            }
            if(!hitAny)throw new Exception("No walkable upper surface detected: "+mesh.name);
            hits++;
        }
        File.WriteAllText(Path.Combine(Work,"ground-verified.txt"),"Verified "+meshes.Length+" static ground colliders and "+hits+" upper-surface ray tests. Edit-mode checks only; player/drop simulation not performed. Time="+DateTime.Now);
        Debug.Log("MAP1_GROUND_VERIFIED");
    }
}
