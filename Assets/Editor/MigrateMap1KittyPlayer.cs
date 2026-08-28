using CatGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MigrateMap1KittyPlayer
{
    private const string ScenePath = "Assets/Scenes/Gamemap1.unity";
    private const string OldPlayerName = "Map1 - Cube Player";
    private const string NewPlayerName = "Kitty_001";

    static MigrateMap1KittyPlayer()
    {
        EditorApplication.delayCall += MigrateIfReady;
    }

    [MenuItem("Tools/Map1/Migrate Player To Kitty_001")]
    public static void MigrateIfReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
            return;

        GameObject oldPlayer = GameObject.Find(OldPlayerName);
        GameObject kitty = FindKittyRoot();
        if (kitty == null)
            return;

        if (oldPlayer == null)
        {
            FixNestedKittyPlayer(kitty);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Move Map1 player to Kitty_001");

        Transform oldTransform = oldPlayer.transform;
        Transform kittyTransform = kitty.transform;
        Camera camera = oldPlayer.GetComponentInChildren<Camera>(true);
        Vector3 cameraLocalPosition = camera != null ? camera.transform.localPosition : Vector3.zero;
        Quaternion cameraLocalRotation = camera != null ? camera.transform.localRotation : Quaternion.identity;
        BoxCollider oldCollider = oldPlayer.GetComponent<BoxCollider>();
        Rigidbody oldBody = oldPlayer.GetComponent<Rigidbody>();

        Undo.RecordObject(kittyTransform, "Position Kitty player");
        kittyTransform.SetPositionAndRotation(oldTransform.position, oldTransform.rotation);

        Rigidbody body = kitty.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(kitty);
        BoxCollider collider = kitty.GetComponent<BoxCollider>();
        if (collider == null) collider = Undo.AddComponent<BoxCollider>(kitty);
        if (oldBody != null)
        {
            body.mass = oldBody.mass;
            body.drag = oldBody.drag;
            body.angularDrag = oldBody.angularDrag;
            body.useGravity = oldBody.useGravity;
            body.isKinematic = oldBody.isKinematic;
            body.interpolation = oldBody.interpolation;
            body.collisionDetectionMode = oldBody.collisionDetectionMode;
            body.constraints = oldBody.constraints;
        }
        if (oldCollider != null)
        {
            Vector3 oldWorldSize = Vector3.Scale(oldCollider.size, oldTransform.lossyScale);
            Vector3 kittyScale = kittyTransform.lossyScale;
            collider.size = new Vector3(
                oldWorldSize.x / Mathf.Max(.0001f, kittyScale.x),
                oldWorldSize.y / Mathf.Max(.0001f, kittyScale.y),
                oldWorldSize.z / Mathf.Max(.0001f, kittyScale.z));
            Vector3 oldWorldCenter = oldTransform.TransformPoint(oldCollider.center);
            collider.center = kittyTransform.InverseTransformPoint(oldWorldCenter);
        }

        if (camera != null)
        {
            Undo.SetTransformParent(camera.transform, kittyTransform, "Attach camera to Kitty player");
            camera.transform.localPosition = cameraLocalPosition;
            camera.transform.localRotation = cameraLocalRotation;
        }

        CubeFirstPersonController controller = kitty.GetComponent<CubeFirstPersonController>();
        if (controller == null) controller = Undo.AddComponent<CubeFirstPersonController>(kitty);
        Renderer kittyRenderer = kitty.GetComponentInChildren<SkinnedMeshRenderer>(true);
        controller.Configure(camera, kittyRenderer, true);

        foreach (AutoFruitPickup fruit in Object.FindObjectsOfType<AutoFruitPickup>(true))
        {
            SerializedObject serializedFruit = new SerializedObject(fruit);
            SerializedProperty player = serializedFruit.FindProperty("player");
            player.objectReferenceValue = kittyTransform;
            serializedFruit.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fruit);
        }

        EditorUtility.SetDirty(kitty);
        Undo.DestroyObjectImmediate(oldPlayer);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("MAP1_KITTY_PLAYER_MIGRATED: Kitty_001 now owns movement, physics, camera, and fruit pickup references.");
    }

    private static GameObject FindKittyRoot()
    {
        foreach (Animator animator in Object.FindObjectsOfType<Animator>(true))
        {
            if (animator.gameObject.name == NewPlayerName)
                return animator.gameObject;
        }
        return null;
    }

    // Older migration revisions selected the identically named mesh child.  Move its
    // player components to the Animator root so movement, camera and cat face share one axis.
    private static void FixNestedKittyPlayer(GameObject kittyRoot)
    {
        CubeFirstPersonController nestedController = null;
        foreach (CubeFirstPersonController controller in kittyRoot.GetComponentsInChildren<CubeFirstPersonController>(true))
        {
            if (controller.gameObject != kittyRoot)
            {
                nestedController = controller;
                break;
            }
        }
        if (nestedController == null || kittyRoot.GetComponent<CubeFirstPersonController>() != null)
            return;

        Transform nested = nestedController.transform;
        Camera camera = nestedController.GetComponentInChildren<Camera>(true);
        Vector3 cameraLocalPosition = camera != null ? camera.transform.localPosition : new Vector3(0f, 2.56f, -2.95f);
        Quaternion cameraLocalRotation = camera != null ? camera.transform.localRotation : Quaternion.identity;
        Rigidbody nestedBody = nested.GetComponent<Rigidbody>();
        BoxCollider nestedCollider = nested.GetComponent<BoxCollider>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Align Map1 Kitty player and camera");
        Undo.RecordObject(kittyRoot.transform, "Align Kitty root");
        kittyRoot.transform.SetPositionAndRotation(nested.position, nested.rotation);

        Rigidbody body = kittyRoot.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(kittyRoot);
        BoxCollider collider = kittyRoot.GetComponent<BoxCollider>();
        if (collider == null) collider = Undo.AddComponent<BoxCollider>(kittyRoot);
        if (nestedBody != null)
        {
            body.mass = nestedBody.mass;
            body.drag = nestedBody.drag;
            body.angularDrag = nestedBody.angularDrag;
            body.useGravity = nestedBody.useGravity;
            body.isKinematic = nestedBody.isKinematic;
            body.interpolation = nestedBody.interpolation;
            body.collisionDetectionMode = nestedBody.collisionDetectionMode;
            body.constraints = nestedBody.constraints;
        }
        if (nestedCollider != null)
        {
            collider.size = nestedCollider.size;
            collider.center = nestedCollider.center;
        }

        if (camera != null)
        {
            Undo.SetTransformParent(camera.transform, kittyRoot.transform, "Attach camera to Kitty root");
            camera.transform.localPosition = cameraLocalPosition;
            camera.transform.localRotation = cameraLocalRotation;
        }

        CubeFirstPersonController rootController = Undo.AddComponent<CubeFirstPersonController>(kittyRoot);
        rootController.Configure(camera, kittyRoot.GetComponentInChildren<SkinnedMeshRenderer>(true), true);

        foreach (AutoFruitPickup fruit in Object.FindObjectsOfType<AutoFruitPickup>(true))
        {
            SerializedObject serializedFruit = new SerializedObject(fruit);
            serializedFruit.FindProperty("player").objectReferenceValue = kittyRoot.transform;
            serializedFruit.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fruit);
        }

        Undo.DestroyObjectImmediate(nestedController);
        if (nestedBody != null) Undo.DestroyObjectImmediate(nestedBody);
        if (nestedCollider != null) Undo.DestroyObjectImmediate(nestedCollider);
        nested.localPosition = Vector3.zero;
        nested.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(kittyRoot);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("MAP1_KITTY_CAMERA_ALIGNED: player, cat face and camera now share the Kitty root direction.");
    }
}
