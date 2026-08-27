using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// Operates on Map1 instances only; never edits the purchased FBX/prefabs.
public static class MakeMap1HousesEnterable
{
    const string Work = @"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string ScenePath = "Assets/Scenes/Gamemap1.unity";
    const string AssetsPath = "Assets/Map1_EnterableHouses";
    const string InteriorName = "Map1 - Interior";
    class Spec
    {
        public int id; public float left, right, top, front, floor;
        public Vector3 expectedMin, expectedMax;
        public Rect[] floors;
        public Bounds Cut { get { return new Bounds(new Vector3((left+right)/2, (floor-.25f+top)/2,front), new Vector3(right-left,top-floor+.25f,2.4f)); } }
    }
    // Local Unity coordinates: FBX X is reflected by the importer.
    static readonly Spec[] Specs = {
        new Spec { id=1,left=-1.0f,right=1.15f,top=2.65f,front=5.5f,floor=.16f,
            expectedMin=new Vector3(-4.11f,-1.23978f,-7.17132f),expectedMax=new Vector3(4.2949f,8.93733f,6.19151f),
            floors=new[]{Rect.MinMaxRect(-3.5f,-6.3f,3.55f,5.55f)} },
        new Spec { id=2,left=-.94f,right=.92f,top=2.35f,front=3.9f,floor=.08f,
            expectedMin=new Vector3(-4.35672f,-1,-4.80355f),expectedMax=new Vector3(4.05472f,5.41f,4.26044f),
            floors=new[]{Rect.MinMaxRect(-2.65f,-4.5f,2.65f,3.98f)} },
        new Spec { id=3,left=-.18f,right=1.65f,top=3.02f,front=3.5f,floor=.63f,
            expectedMin=new Vector3(-7.0523f,-.50588f,-3.84586f),expectedMax=new Vector3(4.88368f,10.19212f,3.8959f),
            floors=new[]{Rect.MinMaxRect(-2.8f,-3.25f,4.2f,3.52f),Rect.MinMaxRect(-6.7f,-3.25f,-2.8f,1.5f)} },
        new Spec { id=5,left=-2.35f,right=-.75f,top=2.57f,front=5.25f,floor=.12f,
            expectedMin=new Vector3(-5.80762f,-1,-5.70315f),expectedMax=new Vector3(5.4548f,8.93733f,5.60373f),
            floors=new[]{Rect.MinMaxRect(-5,-4.85f,1.8f,5.26f),Rect.MinMaxRect(1.8f,-4.85f,4.9f,1.9f)} }
    };
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        var file=Path.Combine(Work,"enterable-houses-request.txt");
        if(!File.Exists(file) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(file);
        try { Build(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"enterable-houses-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Make Houses Enterable")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open Gamemap1 in Edit mode first.");
        if(scene.isDirty) throw new InvalidOperationException("Save your scene first, then choose Tools > Map1 > Make Houses Enterable.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).ToArray();
        var houses=all.Where(f=>f.name.StartsWith("rpgpp_lt_building_") && f.gameObject.activeInHierarchy).ToArray();
        if(houses.Length==0) throw new InvalidOperationException("No village buildings found.");
        var mapping=new Dictionary<MeshFilter,Spec>();
        foreach(var house in houses)
        {
            var s=Specs.FirstOrDefault(p=>house.name.StartsWith("rpgpp_lt_building_"+p.id.ToString("00")));
            if(s==null || house.transform.Find(InteriorName)!=null) throw new InvalidOperationException("Unsupported/already converted house: "+house.name);
            if(house.sharedMesh==null || !house.sharedMesh.isReadable || house.sharedMesh.subMeshCount!=1) throw new InvalidOperationException("Unexpected source mesh: "+house.name);
            if(Vector3.Distance(house.sharedMesh.bounds.min,s.expectedMin)>.025f || Vector3.Distance(house.sharedMesh.bounds.max,s.expectedMax)>.025f)
                throw new InvalidOperationException("Imported coordinates do not match inspected model; nothing changed: "+house.name+" "+house.sharedMesh.bounds);
            if(Vector3.Angle(house.transform.up,Vector3.up)>.1f || (house.transform.lossyScale-Vector3.one).sqrMagnitude>.001f)
                throw new InvalidOperationException("House must have upright unit scale: "+house.name);
            mapping.Add(house,s);
        }
        var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled && !c.isTrigger && c.name.Contains("terrain_")).ToArray();
        if(ground.Length==0) throw new InvalidOperationException("Ground colliders missing.");
        var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CharacterController>()).FirstOrDefault();
        float playerRadius=player!=null ? player.radius+.04f : .64f;
        float playerHeight=player!=null ? player.height : 1.4f;
        if(mapping.Values.Any(s=>s.right-s.left<2*playerRadius+.1f)) throw new InvalidOperationException("Player is too wide for an original doorway.");
        Physics.SyncTransforms();
        Directory.CreateDirectory(Work);
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.before-enterable-houses-"+stamp+".unity"));
        if(!AssetDatabase.IsValidFolder(AssetsPath)) AssetDatabase.CreateFolder("Assets","Map1_EnterableHouses");
        string runFolder=AssetsPath+"/Build_"+stamp;
        AssetDatabase.CreateFolder(AssetsPath,"Build_"+stamp);
        Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Open Map1 houses and add interiors");
        var report=new List<string>();
        var newMeshes=new Dictionary<int,Mesh>();
        bool saved=false;
        try
        {
            var wood=new Material(Shader.Find("Standard")){name="Interior timber",color=new Color(.36f,.24f,.13f)};
            wood.SetFloat("_Glossiness",.12f);AssetDatabase.CreateAsset(wood,runFolder+"/InteriorTimber.mat");
            foreach(var entry in mapping)
            {
                var house=entry.Key;var s=entry.Value;var t=house.transform;
                float floor=s.floor;
                foreach(var rect in s.floors)
                    for(int x=0;x<3;x++) for(int z=0;z<3;z++)
                    {
                        var p=t.TransformPoint(new Vector3(Mathf.Lerp(rect.xMin+.2f,rect.xMax-.2f,x/2f),0,Mathf.Lerp(rect.yMin+.2f,rect.yMax-.2f,z/2f)));
                        float y=GroundY(ground,p);
                        if(!float.IsNegativeInfinity(y)) floor=Mathf.Max(floor,y-t.position.y+.05f);
                    }
                if(s.top-floor<playerHeight+.15f) throw new InvalidOperationException("Ground too high for doorway clearance: "+house.name);
                Mesh mesh;
                if(!newMeshes.TryGetValue(s.id,out mesh))
                {
                    mesh=OpenMesh(house.sharedMesh,s.Cut);mesh.name="Building_"+s.id.ToString("00")+"_Open_DoubleSided";
                    AssetDatabase.CreateAsset(mesh,runFolder+"/"+mesh.name+".asset");newMeshes.Add(s.id,mesh);
                }
                Undo.RecordObject(house,"Replace instance mesh");house.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                // Keep only the new concave shell collision on this instance.
                foreach(var old in house.GetComponents<Collider>()) { Undo.RecordObject(old,"Disable closed house collision");old.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(old); }
                var shell=Undo.AddComponent<MeshCollider>(house.gameObject);shell.sharedMesh=mesh;shell.convex=false;shell.isTrigger=false;
                var interior=new GameObject(InteriorName);Undo.RegisterCreatedObjectUndo(interior,"Create house interior");interior.transform.SetParent(t,false);
                foreach(var r in s.floors)
                    Box(interior.transform,"Timber floor",new Vector3(r.center.x,floor-.10f,r.center.y),new Vector3(r.width,.2f,r.height),wood);
                float cx=(s.left+s.right)*.5f;
                // Continuous threshold through the aperture; does not close the opening.
                Box(interior.transform,"Doorway threshold",new Vector3(cx,floor-.10f,s.front),new Vector3(s.right-s.left,.2f,1.5f),wood);
                float start=s.front+.65f;
                float length=2.5f;
                float low=GroundY(ground,t.TransformPoint(new Vector3(cx,0,start+length)));
                if(float.IsNegativeInfinity(low))throw new InvalidOperationException("No ground at entrance of "+house.name);
                low=low-t.position.y+.015f;
                if(Mathf.Abs(floor-low)>length*.65f)throw new InvalidOperationException("Entrance slope too steep: "+house.name);
                var ramp=Ramp(s.right-s.left+.3f,length,floor,low);ramp.name="EntranceRamp_"+house.GetInstanceID();
                AssetDatabase.CreateAsset(ramp,runFolder+"/"+ramp.name+".asset");
                var go=new GameObject("Entrance ramp",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));Undo.RegisterCreatedObjectUndo(go,"Create entrance ramp");
                go.transform.SetParent(interior.transform,false);go.transform.localPosition=new Vector3(cx,0,start);
                go.GetComponent<MeshFilter>().sharedMesh=ramp;go.GetComponent<MeshRenderer>().sharedMaterial=wood;go.GetComponent<MeshCollider>().sharedMesh=ramp;
                var lightGO=new GameObject("Interior warm light",typeof(Light));Undo.RegisterCreatedObjectUndo(lightGO,"Create interior light");lightGO.transform.SetParent(interior.transform,false);
                var center=s.floors[0].center;lightGO.transform.localPosition=new Vector3(center.x,floor+1.85f,center.y);
                var light=lightGO.GetComponent<Light>();light.type=LightType.Point;light.range=9;light.intensity=.65f;light.color=new Color(1,.84f,.64f);light.shadows=LightShadows.None;
                Physics.SyncTransforms();
                // Sample the full capsule at the doorway: a center ray alone misses narrow openings.
                for(int i=0;i<=12;i++)
                {
                    var feet=t.TransformPoint(new Vector3(cx,floor+.06f,s.front+1.0f-i*.2f));
                    var overlaps=Physics.OverlapCapsule(feet+Vector3.up*playerRadius,feet+Vector3.up*(playerHeight-playerRadius),playerRadius,~0,QueryTriggerInteraction.Ignore);
                    if(overlaps.Contains(shell))throw new InvalidOperationException("Doorway capsule blocked in "+house.name+" at "+i);
                }
                foreach(var r in s.floors)
                {
                    var p=t.TransformPoint(new Vector3(r.center.x,floor+1,r.center.y));
                    bool supported=interior.GetComponentsInChildren<BoxCollider>().Any(c=>c.Raycast(new Ray(p,Vector3.down),out var hit,2));
                    if(!supported)throw new InvalidOperationException("Interior floor test failed: "+house.name);
                }
                report.Add(house.name+": doorway "+(s.right-s.left).ToString("F2")+"m; floor local Y="+floor.ToString("F2")+"; capsule clearance and floor rays passed.");
                Preview(t,new Vector3(cx,floor+2.2f,s.front+5),new Vector3(cx,floor+1.1f,s.front),Path.Combine(Work,"house-open-"+house.GetInstanceID()+".png"));
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save Map1.");
            saved=true;
            Undo.CollapseUndoOperations(group);
            File.Copy(ScenePath,Path.Combine(Work,"Gamemap1.enterable-houses.unity"),true);
            report.Insert(0,"Saved "+houses.Length+" enterable houses. Original FBX and prefabs unchanged. "+DateTime.Now);
            report.Add("Edit-mode geometry checks passed; actual player walkthrough still required.");
            File.WriteAllLines(Path.Combine(Work,"enterable-houses-ready.txt"),report);
            Debug.Log(string.Join("\n",report));
        }
        catch
        {
            if(saved) throw; // Never delete assets referenced by an already saved scene.
            Undo.RevertAllDownToGroup(group);
            // Delete only assets generated by this failed run, never source assets.
            AssetDatabase.DeleteAsset(runFolder);
            throw;
        }
    }
    static void Preview(Transform house,Vector3 from,Vector3 to,string file)
    {
        var go=new GameObject("House preview camera"){hideFlags=HideFlags.HideAndDontSave};
        var rt=RenderTexture.GetTemporary(800,600,24);var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.fieldOfView=65;
            go.transform.position=house.TransformPoint(from);go.transform.LookAt(house.TransformPoint(to));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image=new Texture2D(800,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
        }
        finally { RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt); }
    }
    static float GroundY(MeshCollider[] ground,Vector3 p)
    {
        float y=float.NegativeInfinity;
        foreach(var c in ground) if(c.Raycast(new Ray(p+Vector3.up*30,Vector3.down),out var hit,70) && hit.normal.y>.65f) y=Mathf.Max(y,hit.point.y);
        return y;
    }
    static void Box(Transform parent,string name,Vector3 position,Vector3 size,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(go,"Add floor");go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    struct V
    {
        public Vector3 p,n;public Vector2 uv,uv2;public Color color;
        public static V Lerp(V a,V b,float f) { return new V{p=Vector3.LerpUnclamped(a.p,b.p,f),n=Vector3.LerpUnclamped(a.n,b.n,f).normalized,uv=Vector2.LerpUnclamped(a.uv,b.uv,f),uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,f),color=Color.LerpUnclamped(a.color,b.color,f)}; }
    }
    static List<V> Half(List<V> input,int axis,float edge,bool above)
    {
        var output=new List<V>();if(input.Count==0)return output;
        var a=input[input.Count-1];float da=(a.p[axis]-edge)*(above?1:-1);
        foreach(var b in input)
        {
            float db=(b.p[axis]-edge)*(above?1:-1);
            if((da>=0)!=(db>=0))output.Add(V.Lerp(a,b,da/(da-db)));
            if(db>=0)output.Add(b);a=b;da=db;
        }
        return output;
    }
    static Mesh OpenMesh(Mesh source,Bounds cut)
    {
        var ps=source.vertices;var ns=source.normals;var us=source.uv;var u2=source.uv2;var cs=source.colors;
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var colors=new List<Color>();var tris=new List<int>();
        Action<List<V>> emit=poly=> {
            for(int j=1;j+1<poly.Count;j++)
            {
                V a=poly[0],b=poly[j],c=poly[j+1];if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-12f)continue;
                var vs=new[]{a,b,c,c,b,a};
                for(int k=0;k<6;k++){var v=vs[k];tris.Add(vertices.Count);vertices.Add(v.p);normals.Add(k<3?v.n:-v.n);uv.Add(v.uv);uv2.Add(v.uv2);colors.Add(v.color);}
            }
        };
        var indices=source.triangles;
        for(int i=0;i<indices.Length;i+=3)
        {
            var remainder=new List<V>();
            for(int k=0;k<3;k++){int j=indices[i+k];remainder.Add(new V{p=ps[j],n=ns.Length==ps.Length?ns[j]:Vector3.up,uv=us.Length==ps.Length?us[j]:Vector2.zero,uv2=u2.Length==ps.Length?u2[j]:Vector2.zero,color=cs.Length==ps.Length?cs[j]:Color.white});}
            // Partition triangle into disjoint outside fragments; discard the inside of the doorway box.
            for(int axis=0;axis<3 && remainder.Count>0;axis++)
            {
                emit(Half(remainder,axis,cut.min[axis],false));remainder=Half(remainder,axis,cut.min[axis],true);
                emit(Half(remainder,axis,cut.max[axis],true));remainder=Half(remainder,axis,cut.max[axis],false);
            }
        }
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);if(u2.Length==ps.Length)mesh.SetUVs(1,uv2);if(cs.Length==ps.Length)mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    static Mesh Ramp(float width,float length,float high,float low)
    {
        float x=width/2;
        var v=new[]{new Vector3(-x,high,0),new Vector3(x,high,0),new Vector3(-x,low,length),new Vector3(x,low,length),new Vector3(-x,high-.18f,0),new Vector3(x,high-.18f,0),new Vector3(-x,low-.18f,length),new Vector3(x,low-.18f,length)};
        var m=new Mesh();m.vertices=v;m.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};m.uv=v.Select(p=>new Vector2(p.x,p.z)).ToArray();m.RecalculateNormals();m.RecalculateBounds();return m;
    }
}
