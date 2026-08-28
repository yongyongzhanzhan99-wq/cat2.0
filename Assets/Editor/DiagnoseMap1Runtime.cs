using System;
using System.IO;
using System.Linq;
using System.Text;
using CatGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only diagnostics. Opt-in marker stays outside Assets and is not committed.
public static class DiagnoseMap1Runtime
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    static double next;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1;
        string request=Path.Combine(Work,"runtime-diagnostics-request.txt");
        if(!File.Exists(request)||DateTime.UtcNow-File.GetLastWriteTimeUtc(request)>TimeSpan.FromMinutes(15))return;
        try { Capture(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Work,"runtime-diagnostics-error.txt"),e.ToString()); }
    }
    [MenuItem("Tools/Map1/Capture Runtime Diagnostics")]
    public static void Capture()
    {
        var s=new StringBuilder();s.AppendLine(DateTime.Now.ToString("O"));
        s.AppendLine("Project="+Application.dataPath);
        s.AppendLine("Playing="+EditorApplication.isPlaying+" Paused="+EditorApplication.isPaused+" Focused="+Application.isFocused+" TimeScale="+Time.timeScale+" FocusedWindow="+(EditorWindow.focusedWindow!=null?EditorWindow.focusedWindow.GetType().Name:"none"));
        s.AppendLine("ActiveScene="+SceneManager.GetActiveScene().path);
        var roots=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(sc=>sc.isLoaded).SelectMany(sc=>sc.GetRootGameObjects()).ToArray();
        var players=roots.SelectMany(g=>g.GetComponentsInChildren<CubeFirstPersonController>(true)).ToArray();
        var ground=roots.SelectMany(g=>g.GetComponentsInChildren<MeshCollider>(true)).Where(c=>c.name.Contains("terrain_")).ToArray();
        s.AppendLine("GroundColliders="+ground.Length+" ActiveEnabled="+ground.Count(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger)+" MissingMeshes="+ground.Count(c=>c.sharedMesh==null));
        foreach(var p in players)
        {
            var c=p.GetComponent<CharacterController>();
            var rb=p.GetComponent<Rigidbody>();
            s.AppendLine("Player="+p.name+" Active="+p.gameObject.activeInHierarchy+" ScriptEnabled="+p.enabled+" Position="+p.transform.position.ToString("F3")+" CC="+(c!=null)+" Rigidbody="+(rb!=null));
            if(rb!=null)s.AppendLine("Velocity="+rb.velocity+" Gravity="+rb.useGravity+" Kinematic="+rb.isKinematic+" DetectCollisions="+rb.detectCollisions);
            else if(c!=null)s.AppendLine("Velocity="+c.velocity+" Grounded="+c.isGrounded+" CCEnabled="+c.enabled);
            s.AppendLine("Input W="+Input.GetKey(KeyCode.W)+" A="+Input.GetKey(KeyCode.A)+" S="+Input.GetKey(KeyCode.S)+" D="+Input.GetKey(KeyCode.D)+" Space="+Input.GetKey(KeyCode.Space));
            float y=float.NegativeInfinity;
            foreach(var g in ground)
                if(g.enabled&&g.gameObject.activeInHierarchy&&g.Raycast(new Ray(p.transform.position+Vector3.up*5,Vector3.down),out var hit,50))y=Mathf.Max(y,hit.point.y);
            s.AppendLine("GroundSurfaceY="+y+" IgnoredGroundLayerPairs="+ground.Count(g=>Physics.GetIgnoreLayerCollision(p.gameObject.layer,g.gameObject.layer)));
        }
        foreach(var c in roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Where(c=>c.enabled&&c.gameObject.activeInHierarchy))s.AppendLine("ActiveCamera="+c.name+" Parent="+(c.transform.parent!=null?c.transform.parent.name:"none"));
        var fruits=roots.SelectMany(g=>g.GetComponentsInChildren<AutoFruitPickup>(true)).ToArray();
        s.AppendLine("Fruits="+fruits.Length+" Collected="+fruits.Count(f=>f.IsCollected));
        foreach(var f in fruits)
        {
            var d=new SerializedObject(f);var player=d.FindProperty("player").objectReferenceValue as Transform;
            var center=f.transform.TransformPoint(d.FindProperty("localPickupPoint").vector3Value);
            var body=player!=null?player.GetComponent<BoxCollider>():null;
            var playerCenter=body!=null?body.bounds.center:player!=null?player.position:Vector3.zero;
            s.AppendLine("Fruit="+f.name+" Active="+f.gameObject.activeInHierarchy+" Enabled="+f.enabled+" Collected="+f.IsCollected+" Target="+(player!=null?player.name:"MISSING")+" Radius="+d.FindProperty("pickupRadius").floatValue+" Center="+center.ToString("F3")+" Distance="+(player!=null?Vector3.Distance(playerCenter,center).ToString("F3"):"unknown"));
        }
        File.WriteAllText(Path.Combine(Work,"runtime-diagnostics-latest.txt"),s.ToString());
        if(EditorApplication.isPlaying)File.AppendAllText(Path.Combine(Work,"runtime-diagnostics-play.log"),s.ToString()+Environment.NewLine);
    }
}
