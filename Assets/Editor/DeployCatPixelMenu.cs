using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CatGame;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DeployCatPixelMenu
{
    const string Work=@"C:\Users\14265\Documents\Codex\2026-08-27\github-github-desktop-unity-unity-hub\work\map1";
    const string Map="Assets/Scenes/Gamemap1.unity";
    const string Art="Assets/Scenes/CatStartUI/PixelMenu.png";
    [InitializeOnLoadMethod] static void Init()
    {
        EditorApplication.delayCall+=Requested;
        EditorSceneManager.sceneOpened-=Opened;EditorSceneManager.sceneOpened+=Opened;
    }
    static void Opened(Scene scene,OpenSceneMode mode){EditorApplication.delayCall+=Requested;}
    static void Requested()
    {
        var request=Path.Combine(Work,"pixel-menu-request.txt");
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().path==Map)return;
        File.Delete(request);
        try{Build();}catch(Exception e){File.WriteAllText(Path.Combine(Work,"pixel-menu-error.txt"),DateTime.Now+"\n"+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Cat/Build Pixel Welcome In New Scene")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path==Map)throw new InvalidOperationException("Open your NEW empty scene in Edit mode; Gamemap1 is protected.");
        if(scene.GetRootGameObjects().Any(g=>g.GetComponent<Camera>()==null&&g.GetComponent<Light>()==null))throw new InvalidOperationException("This scene already contains content. Open the new empty scene so nothing is overwritten.");
        string path=string.IsNullOrEmpty(scene.path)?"Assets/Scenes/CatStart.unity":scene.path;
        if(!path.StartsWith("Assets/Scenes/"))throw new InvalidOperationException("Save the new scene inside Assets/Scenes first.");
        if(string.IsNullOrEmpty(scene.path)&&File.Exists(path))throw new InvalidOperationException("CatStart already exists. Save your new scene with a different name first.");
        string originalHash=Hash(Map);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if(font==null)throw new InvalidOperationException("TMP essential font missing.");
        AssetDatabase.ImportAsset(Art,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(Art) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Pixel art asset is missing.");
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art);
        if(sprite==null)throw new InvalidOperationException("Pixel art could not be imported.");
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if(File.Exists(path))File.Copy(path,Path.Combine(Work,"StartScene.before-pixel-"+stamp+".unity"));
        File.Copy("ProjectSettings/EditorBuildSettings.asset",Path.Combine(Work,"BuildSettings.before-pixel-"+stamp+".asset"));
        var oldBuild=EditorBuildSettings.scenes;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Deploy Cat pixel welcome menu");bool saved=false;
        try
        {
            var root=new GameObject("Cat - Pixel Welcome",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));Undo.RegisterCreatedObjectUndo(root,"Create pixel menu");
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var backing=UI(root.transform,"Forest letterbox",Vector2.zero,Vector2.one);var dark=backing.gameObject.AddComponent<Image>();dark.color=new Color(.025f,.095f,.065f);dark.raycastTarget=false;
            var picture=UI(root.transform,"Approved Cat pixel artwork",new Vector2(.5f,.5f),new Vector2(.5f,.5f));picture.sizeDelta=sprite.rect.size;
            var illustration=picture.gameObject.AddComponent<Image>();illustration.sprite=sprite;illustration.raycastTarget=false;
            var fitter=picture.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=sprite.rect.width/sprite.rect.height;
            // This rectangle is aligned with the actual Welcome plaque in the approved illustration.
            var hit=UI(picture,"Welcome button",new Vector2(.315f,.105f),new Vector2(.686f,.274f));
            var tint=hit.gameObject.AddComponent<Image>();tint.color=Color.white;tint.raycastTarget=true;
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=tint;
            var colors=button.colors;colors.normalColor=new Color(1,1,1,0);colors.highlightedColor=new Color(1,.94f,.65f,.12f);colors.selectedColor=new Color(1,.95f,.7f,.06f);colors.pressedColor=new Color(.2f,.1f,.03f,.2f);colors.disabledColor=new Color(.1f,.1f,.1f,.18f);colors.fadeDuration=.1f;button.colors=colors;
            var statusRect=UI(picture,"Loading and error status",new Vector2(.23f,.02f),new Vector2(.77f,.09f));
            var status=statusRect.gameObject.AddComponent<TextMeshProUGUI>();status.font=font;status.text="";status.fontSize=24;status.alignment=TextAlignmentOptions.Center;status.color=new Color(1,.97f,.8f);status.fontStyle=FontStyles.Bold;status.raycastTarget=false;
            var menu=root.AddComponent<CatStartMenu>();menu.Configure(button,null,status);UnityEventTools.AddPersistentListener(button.onClick,menu.StartGame);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));Undo.RegisterCreatedObjectUndo(events,"Create menu input");events.GetComponent<EventSystem>().firstSelectedGameObject=button.gameObject;
            if(!scene.GetRootGameObjects().Any(g=>g.GetComponent<Camera>()!=null))
            {
                var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));Undo.RegisterCreatedObjectUndo(camera,"Create menu camera");camera.tag="MainCamera";
            }
            var build=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(path,true)};
            foreach(var item in oldBuild.Where(b=>b.path!=path))build.Add(new EditorBuildSettingsScene(item.path,item.path==Map||item.enabled));
            if(!build.Any(b=>b.path==Map))build.Add(new EditorBuildSettingsScene(Map,true));
            EditorBuildSettings.scenes=build.ToArray();
            Canvas.ForceUpdateCanvases();
            if(button.onClick.GetPersistentEventCount()!=1||new SerializedObject(menu).FindProperty("targetScenePath").stringValue!=Map)throw new InvalidOperationException("Button link verification failed.");
            Preview(canvas);
            if(Hash(Map)!=originalHash)throw new InvalidOperationException("Game scene changed unexpectedly; refusing completion.");
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Could not save start scene.");
            saved=true;Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Work,"pixel-menu-ready.txt"),"Saved pixel menu: "+path+"\nWelcome -> "+Map+". Start menu is first enabled Build Settings scene. Button wiring checked. Original game scene hash unchanged: "+originalHash+"\nPlay-mode click-through test pending. "+DateTime.Now);
            Selection.activeGameObject=root;Debug.Log("Cat pixel start menu ready; original game scene unchanged.");
        }
        catch{if(!saved){Undo.RevertAllDownToGroup(undo);EditorBuildSettings.scenes=oldBuild;}throw;}
    }
    static RectTransform UI(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    static void Preview(Canvas canvas)
    {
        var go=new GameObject("Temporary menu preview"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.enabled=false;
        var rt=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;Texture2D texture=null;float oldDistance=canvas.planeDistance;
        try{camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Work,"pixel-menu-preview.png"),texture.EncodeToPNG());}
        finally{canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.planeDistance=oldDistance;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(rt);}
    }
}
