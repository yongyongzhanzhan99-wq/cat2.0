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
        [SerializeField, Min(.1f)] private float walkSpeed=4f;
        [SerializeField, Min(.1f)] private float jumpHeight=1.2f;
        [SerializeField] private float gravity=-22f;
        [SerializeField, Min(1f)] private float keyboardTurnSpeed=110f;
        private Rigidbody body;
        private BoxCollider shape;
        private PhysicMaterial movementMaterial;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float movement,turn;
        private bool jumpRequested,resetRequested;
        public bool IsGrounded { get; private set; }
        public void Configure(Camera camera,Renderer renderer) { viewCamera=camera;cubeBody=renderer; }
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
            movementMaterial=new PhysicMaterial("Player low friction"){dynamicFriction=0,staticFriction=0,bounciness=0,frictionCombine=PhysicMaterialCombine.Minimum,bounceCombine=PhysicMaterialCombine.Minimum};
            shape.material=movementMaterial;
            if(cubeBody!=null)cubeBody.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void Update()
        {
            if(!Application.isFocused){movement=turn=0;jumpRequested=resetRequested=false;return;}
            movement=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            turn=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            jumpRequested|=Input.GetKeyDown(KeyCode.Space);
            resetRequested|=Input.GetKeyDown(KeyCode.R);
        }
        private void FixedUpdate()
        {
            if(resetRequested||body.position.y<spawnPosition.y-15){Respawn();return;}
            if(!Application.isFocused){movement=turn=0;jumpRequested=false;}
            var rotation=body.rotation*Quaternion.Euler(0,turn*keyboardTurnSpeed*Time.fixedDeltaTime,0);
            body.angularVelocity=Vector3.zero;body.MoveRotation(rotation);
            Vector3 normal;IsGrounded=FindGround(out normal)&&body.velocity.y<=.3f;
            Vector3 horizontal=rotation*Vector3.forward*(movement*walkSpeed);
            var velocity=new Vector3(horizontal.x,body.velocity.y,horizontal.z);
            if(IsGrounded&&Mathf.Abs(movement)>.01f)
            {
                var slope=Vector3.ProjectOnPlane(horizontal,normal).normalized*(Mathf.Abs(movement)*walkSpeed);
                velocity=new Vector3(slope.x,slope.y,slope.z);
            }
            if(jumpRequested&&IsGrounded){velocity.y=Mathf.Sqrt(2*Mathf.Abs(gravity)*jumpHeight);IsGrounded=false;}
            jumpRequested=false;body.velocity=velocity;
            // Keep built-in gravity enabled; add only the difference from the former jump tuning.
            body.AddForce(Vector3.up*(gravity-Physics.gravity.y),ForceMode.Acceleration);
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
