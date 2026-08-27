using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public static class RecoverMap1
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += RunIfRequested; }
    static void RunIfRequested()
    {
        string request=Path.Combine(Work,"recover-request.txt");
        if(!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Restore(); } catch(Exception e) { File.WriteAllText(Path.Combine(Work,"recovery-error.txt"),e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Map1/Restore Verified Auto Backup")]
    public static void Restore()
    {
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("An open scene has unsaved changes. Recovery paused to protect them.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Gamemap1_Recovered.unity",OpenSceneMode.Single);
        var fruits=GameObject.Find("Map1 - Pathside Fruits");
        var prompt=GameObject.Find("Map1 - Floating Entrance Prompt");
        if(fruits==null || fruits.transform.childCount!=13 || prompt==null) throw new InvalidOperationException("Auto backup does not contain the expected 13 fruits and prompt.");
        var text=prompt.GetComponent<TextMeshPro>();
        if(text==null || text.font==null || !text.text.Contains("Follow the paths")) throw new InvalidOperationException("Prompt/font restoration failed.");
        var rs=fruits.GetComponentsInChildren<Renderer>();
        if(rs.Length<13 || rs.Any(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null || !m.shader.isSupported)))
            throw new InvalidOperationException("A fruit model or supported material is missing.");
        foreach(var r in rs) foreach(var m in r.sharedMaterials)
            if(m.shader.name=="Hidden/InternalErrorShader" || m.mainTexture==null) throw new InvalidOperationException("Fruit shader or texture is invalid.");
        text.ForceMeshUpdate();
        if(!EditorSceneManager.SaveScene(scene,"Assets/Scenes/Gamemap1.unity")) throw new IOException("Cannot save restored Map1.");
        File.Copy("Assets/Scenes/Gamemap1.unity",Path.Combine(Work,"Gamemap1.restored-verified.unity"),true);
        var c=GameObject.Find("Main camera").GetComponent<Camera>();
        var prev=c.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(1600,1000,24); var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try { c.targetTexture=rt; c.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Work,"restored-preview.png"),tex.EncodeToPNG()); }
        finally { c.targetTexture=prev;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt); }
        File.WriteAllText(Path.Combine(Work,"recovery-success.txt"),"Restored and saved Gamemap1. 13 fruits, supported shaders, textures and prompt font verified. "+DateTime.Now);
        Debug.Log("MAP1_RECOVERY_VERIFIED");
    }
}
