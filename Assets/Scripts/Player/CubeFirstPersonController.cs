using UnityEngine;

namespace CatGame
{
    // Keep the existing class/asset GUID so scene and editor-tool references survive migration.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CubeFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Renderer cubeBody;
        [SerializeField] private bool useExistingKittyVisual;
        [SerializeField, Min(.1f)] private float walkSpeed=4f;
        [SerializeField, Min(.1f)] private float jumpHeight=1.2f;
        [SerializeField] private float gravity=-22f;
        [SerializeField, Min(1f)] private float keyboardTurnSpeed=110f;
        private Rigidbody body;
        private BoxCollider shape;
        private PhysicMaterial movementMaterial;
        private Animator catAnimator;
        private float idleStartedAt;
        private bool wasMoving;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float movement,turn;
        private bool isRunning,runMode;
        private bool jumpRequested,resetRequested;
        public bool IsGrounded { get; private set; }
        public void Configure(Camera camera,Renderer renderer) { viewCamera=camera;cubeBody=renderer; }
        public void Configure(Camera camera,Renderer renderer,bool existingKittyVisual)
        {
            viewCamera=camera;
            cubeBody=renderer;
            useExistingKittyVisual=existingKittyVisual;
        }
        private void Awake()
        {
            body=GetComponent<Rigidbody>();shape=GetComponent<BoxCollider>();
            if(body==null||shape==null){Debug.LogError("Rigidbody player migration is required before Play.",this);enabled=false;return;}
            if(viewCamera==null)viewCamera=GetComponentInChildren<Camera>();
            if(viewCamera==null){Debug.LogError("Player camera is missing.",this);enabled=false;return;}
            spawnPosition=body.position;spawnRotation=body.rotation;
            body.isKinematic=false;body.useGravity=true;body.detectCollisions=true;
            body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            EnsureMap1GroundColliders();
            movementMaterial=new PhysicMaterial("Player low friction"){dynamicFriction=0,staticFriction=0,bounciness=0,frictionCombine=PhysicMaterialCombine.Minimum,bounceCombine=PhysicMaterialCombine.Minimum};
            shape.material=movementMaterial;
            CreateCatVisual();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }

        // The scene player remains the physics root.  Kitty is only its visual child,
        // so the prefab's old CharacterController/input scripts cannot fight this controller.
        private void CreateCatVisual()
        {
            if (useExistingKittyVisual)
            {
                catAnimator = GetComponentInChildren<Animator>(true);
                foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != this) behaviour.enabled = false;
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                    if (collider != shape) collider.enabled = false;
                if (catAnimator == null)
                    Debug.LogError("The scene Kitty_001 has no Animator.", this);
                else
                {
                    ResetCatToInitialPose();
                    idleStartedAt = Time.unscaledTime;
                }
                return;
            }

            if (cubeBody != null)
                cubeBody.enabled = false;

            var kittyPrefab = Resources.Load<GameObject>("Kitty_001");
            if (kittyPrefab == null)
            {
                Debug.LogError("Kitty_001 prefab is missing from Assets/Resources.", this);
                return;
            }

            var kitty = Instantiate(kittyPrefab, transform, false);
            kitty.name = "Kitty Player Visual";
            kitty.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            kitty.transform.localRotation = Quaternion.identity;
            kitty.transform.localScale = Vector3.one * 3.5f;

            foreach (var behaviour in kitty.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (var collider in kitty.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            catAnimator = kitty.GetComponentInChildren<Animator>(true);
            if (catAnimator == null)
            {
                Debug.LogError("Kitty Player Visual has no Animator.", kitty);
                return;
            }

            ResetCatToInitialPose();
            idleStartedAt = Time.unscaledTime;

            var map1Texture = Resources.Load<Texture2D>("KittyMap1Texture");
            if (map1Texture == null)
            {
                Debug.LogError("KittyMap1Texture is missing from Assets/Resources.", kitty);
                return;
            }

            var materialProperties = new MaterialPropertyBlock();
            foreach (var renderer in kitty.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.GetPropertyBlock(materialProperties);
                materialProperties.SetTexture("_MainTex", map1Texture);
                materialProperties.SetTexture("_BaseMap", map1Texture);
                renderer.SetPropertyBlock(materialProperties);
            }
        }

        private void EnsureMap1GroundColliders()
        {
            int repaired = 0;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "Terrain" && root.name != "Path")
                    continue;

                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy)
                        continue;

                    bool hasSolidCollider = false;
                    foreach (var existing in filter.GetComponents<Collider>())
                    {
                        if (existing.enabled && !existing.isTrigger)
                        {
                            hasSolidCollider = true;
                            break;
                        }
                    }
                    if (hasSolidCollider)
                        continue;

                    var collider = filter.GetComponent<MeshCollider>();
                    if (collider == null)
                        collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.isTrigger = false;
                    collider.enabled = true;
                    repaired++;
                }
            }

            if (repaired > 0)
                Debug.Log("Map1: repaired " + repaired + " missing terrain/path colliders.");
            Physics.SyncTransforms();
        }
        private void Update()
        {
            if(!Application.isFocused){movement=turn=0;jumpRequested=resetRequested=false;return;}
            movement=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            turn=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            if(Input.GetKeyDown(KeyCode.LeftShift))runMode=!runMode;
            isRunning=Mathf.Abs(movement)>.01f&&runMode;
            jumpRequested|=Input.GetKeyDown(KeyCode.Space);
            resetRequested|=Input.GetKeyDown(KeyCode.R);
            UpdateCatAnimation();
        }

        private void UpdateCatAnimation()
        {
            if (catAnimator == null)
                return;

            bool isMoving = Mathf.Abs(movement) > .01f;
            if (isMoving)
            {
                catAnimator.enabled = true;
                catAnimator.speed = 1f;
                catAnimator.SetFloat("Vert", Mathf.Abs(movement));
                catAnimator.SetFloat("State", isRunning ? 1f : 0f);
                idleStartedAt = Time.unscaledTime;
            }
            else
            {
                if (wasMoving)
                {
                    idleStartedAt = Time.unscaledTime;
                    ResetCatToInitialPose();
                }

                if (Time.unscaledTime - idleStartedAt >= 15f)
                {
                    catAnimator.SetFloat("Vert", 0f);
                    catAnimator.SetFloat("State", 0f);
                    catAnimator.enabled = true;
                    catAnimator.speed = 1f;
                }
                else
                {
                    catAnimator.enabled = false;
                }
            }
            wasMoving = isMoving;
        }

        // Reset to the model's initial pose instead of freezing the current walk/run frame.
        private void ResetCatToInitialPose()
        {
            if (catAnimator == null)
                return;
            catAnimator.enabled = true;
            catAnimator.Rebind();
            catAnimator.Update(0f);
            catAnimator.enabled = false;
        }
        private void FixedUpdate()
        {
            if(resetRequested||body.position.y<spawnPosition.y-15){Respawn();return;}
            if(!Application.isFocused){movement=turn=0;jumpRequested=false;}
            var rotation=body.rotation*Quaternion.Euler(0,turn*keyboardTurnSpeed*Time.fixedDeltaTime,0);
            body.angularVelocity=Vector3.zero;body.MoveRotation(rotation);
            Vector3 normal;IsGrounded=FindGround(out normal)&&body.velocity.y<=.3f;
            float speed=walkSpeed*(isRunning?1.5f:1f);
            Vector3 horizontal=rotation*Vector3.forward*(movement*speed);
            var velocity=new Vector3(horizontal.x,body.velocity.y,horizontal.z);
            if(IsGrounded&&Mathf.Abs(movement)>.01f)
            {
                var slope=Vector3.ProjectOnPlane(horizontal,normal).normalized*(Mathf.Abs(movement)*speed);
                velocity=new Vector3(slope.x,slope.y,slope.z);
            }
            if(jumpRequested&&IsGrounded){velocity.y=Mathf.Sqrt(2*Mathf.Abs(gravity)*jumpHeight);IsGrounded=false;}
            jumpRequested=false;body.velocity=velocity;
            // Keep built-in gravity enabled; add only the difference from the former jump tuning.
            body.AddForce(Vector3.up*(gravity-Physics.gravity.y),ForceMode.Acceleration);
        }

        private void LateUpdate()
        {
            if (viewCamera == null)
                return;

            // The camera stays behind the player, but always looks back at the player's centre.
            // This makes A/D orbit around the character instead of an offset point behind it.
            viewCamera.transform.LookAt(transform.position + Vector3.up * 0.7f);
        }
        private bool FindGround(out Vector3 normal)
        {
            normal=Vector3.up;
            var center=transform.TransformPoint(shape.center);
            var half=Vector3.Scale(shape.size,transform.lossyScale)*.48f;
            float nearest=float.PositiveInfinity;bool found=false;
            foreach(var hit in Physics.BoxCastAll(center,half,Vector3.down,body.rotation,.12f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.attachedRigidbody==body||hit.normal.y<.65f||Physics.GetIgnoreLayerCollision(gameObject.layer,hit.collider.gameObject.layer)||hit.distance>=nearest)continue;
                normal=hit.normal;nearest=hit.distance;found=true;
            }
            return found;
        }
        private void Respawn()
        {
            body.position=spawnPosition;body.rotation=spawnRotation;body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            movement=turn=0;jumpRequested=resetRequested=false;IsGrounded=false;viewCamera.transform.localRotation=Quaternion.identity;
        }
        private void OnDisable(){movement=turn=0;jumpRequested=resetRequested=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        private void OnDestroy(){if(movementMaterial!=null)Destroy(movementMaterial);}
        private void OnGUI()
        {
            GUI.Box(new Rect(12,12,505,54),"W: forward   S: backward   A/D: turn   Space: jump\nR: respawn   Rigidbody physics");
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-10,20,20),"+");
        }
    }
}
