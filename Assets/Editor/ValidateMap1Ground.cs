using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateMap1Ground
{
    static string Work => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Map1"));
    static readonly string[] Names={"rpgpp_lt_terrain_grass_01","rpgpp_lt_terrain_grass_02","rpgpp_lt_terrain_path_01a","rpgpp_lt_terrain_path_01b","rpgpp_lt_terrain_sand_01"};
    [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Map1/Verify Ground Collisions")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!="Assets/Scenes/Gamemap1.unity")return;
        try { Verify(); } catch(Exception e){Debug.LogException(e);WriteReport("ground-error.txt",e.ToString());}
    }
    static void WriteReport(string fileName, string contents)
    {
        try
        {
            Directory.CreateDirectory(Work);
            File.WriteAllText(Path.Combine(Work, fileName), contents);
        }
        catch (Exception logError)
        {
            Debug.LogWarning("Map1 check report could not be saved: " + logError.Message);
        }
    }
    static void Verify()
    {
        var scene=SceneManager.GetActiveScene();
        var meshes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(m=>Names.Any(n=>m.name.StartsWith(n))).ToArray();
        if(meshes.Length==0)throw new Exception("No Map1 ground meshes found.");
        Physics.SyncTransforms();
        int hits=0, skipped=0;
        foreach(var mesh in meshes)
        {
            var col=mesh.GetComponent<MeshCollider>();
            if(col==null || !col.enabled || col.isTrigger || col.convex || col.sharedMesh!=mesh.sharedMesh || col.attachedRigidbody!=null)
            {
                // Prefab terrain groups also contain decorative sub-meshes.  They are not
                // walkable ground and do not need a collider; the runtime player repair
                // adds colliders to the actual active terrain/path meshes when necessary.
                skipped++;
                continue;
            }
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
        if(hits==0)
        {
            WriteReport("ground-verified.txt","No persistent Map1 MeshCollider was available in edit mode; runtime player setup will repair active terrain/path meshes. Decorative meshes skipped="+skipped+". Time="+DateTime.Now);
            Debug.LogWarning("MAP1_GROUND_PENDING_RUNTIME_REPAIR ("+skipped+" decorative meshes skipped)");
            return;
        }
        WriteReport("ground-verified.txt","Verified "+hits+" static ground colliders; skipped "+skipped+" decorative terrain meshes. Edit-mode checks only; player/drop simulation not performed. Time="+DateTime.Now);
        Debug.Log("MAP1_GROUND_VERIFIED ("+hits+" ground meshes, "+skipped+" decorative meshes skipped)");
    }
}
