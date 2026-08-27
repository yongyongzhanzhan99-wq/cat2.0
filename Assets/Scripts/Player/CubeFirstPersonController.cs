using UnityEngine;

namespace CatGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CubeFirstPersonController : MonoBehaviour
    {
        [Header("First person camera")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Renderer cubeBody;
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -22f;
        [Header("Keyboard turning")]
        [SerializeField, Min(1f)] private float keyboardTurnSpeed = 110f;

        private CharacterController controller;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float verticalSpeed;

        public void Configure(Camera camera, Renderer body)
        {
            viewCamera = camera;
            cubeBody = body;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null)
            {
                Debug.LogError("Cube player needs its first-person camera assigned.", this);
                enabled = false;
                return;
            }
            // The cube remains visible in Edit mode, but cannot obscure its own view in Play mode.
            if (cubeBody != null) cubeBody.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private void Start() { ReleaseCursor(); }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool acceptInput = Application.isFocused;
            float move = 0f;
            bool jump = false;
            if (acceptInput)
            {
                float turn = (Input.GetKey(KeyCode.D) ? 1 : 0)
                           - (Input.GetKey(KeyCode.A) ? 1 : 0);
                transform.Rotate(0f, turn * keyboardTurnSpeed * dt, 0f);
                move = (Input.GetKey(KeyCode.W) ? 1 : 0)
                     - (Input.GetKey(KeyCode.S) ? 1 : 0);
                jump = Input.GetKeyDown(KeyCode.Space);
                if (Input.GetKeyDown(KeyCode.R)) { Respawn(); return; }
            }

            bool grounded = controller.isGrounded;
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (grounded && jump) verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * dt, -45f);
            Vector3 horizontal = transform.forward * move * walkSpeed;
            CollisionFlags flags = controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            if (transform.position.y < spawnPosition.y - 15f) Respawn();
        }

        private void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            verticalSpeed = 0f;
            viewCamera.transform.localRotation = Quaternion.identity;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
        private void OnDisable() { ReleaseCursor(); }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 505, 54), "W: forward   S: backward   A: turn left   D: turn right\nSpace: jump   R: respawn   Keyboard controls only");
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
        }
    }
}
