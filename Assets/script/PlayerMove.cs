using UnityEngine;

/// <summary>
/// Map2 玩家控制器。行为与 Map1 保持一致：W/S 前后移动、A/D 以角色为中心转向、Space 跳跃。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 110f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -22f;

    private Rigidbody rb;
    private Collider playerCollider;
    private PhysicMaterial movementMaterial;
    private Camera viewCamera;
    private Vector3 cameraLocalOffset;
    private Animator catAnimator;
    private float movement;
    private float turn;
    private bool isRunning;
    private bool runMode;
    private bool jumpRequested;
    private bool resetRequested;
    private bool isGrounded;
    private bool vehiclePassenger;
    private float idleStartedAt;
    private bool wasMoving;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        spawnPosition = rb.position;
        spawnRotation = rb.rotation;
        viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera != null)
        {
            // The scene contained an old MouseLook component that followed a different,
            // disabled demo player.  It must not override the character-centred orbit.
            foreach (MonoBehaviour cameraBehaviour in viewCamera.GetComponents<MonoBehaviour>())
                cameraBehaviour.enabled = false;
            cameraLocalOffset = viewCamera.transform.localPosition;
        }
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        movementMaterial = new PhysicMaterial("Player low friction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
        playerCollider.material = movementMaterial;
        CreateCatVisual();
    }

    private void Update()
    {
        if (vehiclePassenger)
        {
            movement = turn = 0f;
            jumpRequested = resetRequested = false;
            if (catAnimator != null)
                catAnimator.enabled = false;
            return;
        }
        if (!Application.isFocused)
        {
            movement = turn = 0f;
            jumpRequested = resetRequested = false;
            return;
        }
        movement = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        turn = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        if (Input.GetKeyDown(KeyCode.LeftShift))
            runMode = !runMode;
        isRunning = Mathf.Abs(movement) > .01f && runMode;
        jumpRequested |= Input.GetKeyDown(KeyCode.Space);
        resetRequested |= Input.GetKeyDown(KeyCode.R);
        UpdateCatAnimation();
    }

    private void FixedUpdate()
    {
        if (vehiclePassenger)
            return;
        if (resetRequested || rb.position.y < spawnPosition.y - 15f)
        {
            Respawn();
            return;
        }
        if (!Application.isFocused)
        {
            movement = turn = 0f;
            jumpRequested = false;
        }
        Quaternion rotation = rb.rotation * Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        rb.angularVelocity = Vector3.zero;
        rb.MoveRotation(rotation);
        isGrounded = FindGround(out var normal) && rb.velocity.y <= .3f;
        float speed = moveSpeed * (isRunning ? 1.5f : 1f);
        Vector3 horizontal = rotation * Vector3.forward * (movement * speed);
        Vector3 velocity = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
        if (isGrounded && Mathf.Abs(movement) > .01f)
        {
            Vector3 slope = Vector3.ProjectOnPlane(horizontal, normal).normalized * (Mathf.Abs(movement) * speed);
            velocity = new Vector3(slope.x, slope.y, slope.z);
        }
        if (jumpRequested && isGrounded)
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        jumpRequested = false;
        rb.velocity = velocity;
        rb.AddForce(Vector3.up * (gravity - Physics.gravity.y), ForceMode.Acceleration);
    }

    private void LateUpdate()
    {
        if (viewCamera == null)
            return;

        // Rebuild the camera position from the player root every frame.  A/D rotates
        // the player root, so this offset rotates around the character—not the camera.
        Vector3 lookPoint = transform.position + Vector3.up * .7f;
        Vector3 rotatedOffset = transform.rotation * new Vector3(cameraLocalOffset.x, 0f, cameraLocalOffset.z);
        viewCamera.transform.position = lookPoint + rotatedOffset + Vector3.up * (cameraLocalOffset.y - .7f);
        viewCamera.transform.rotation = Quaternion.LookRotation(lookPoint - viewCamera.transform.position, Vector3.up);
    }

    private bool FindGround(out Vector3 normal)
    {
        normal = Vector3.up;
        if (playerCollider == null)
            return false;
        Bounds bounds = playerCollider.bounds;
        Vector3 half = bounds.extents * .94f;
        Vector3 center = bounds.center;
        float nearest = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in Physics.BoxCastAll(center, half, Vector3.down, rb.rotation, .12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody == rb || hit.normal.y < .65f || hit.distance >= nearest)
                continue;
            normal = hit.normal;
            nearest = hit.distance;
            found = true;
        }
        return found;
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

    private void ResetCatToInitialPose()
    {
        if (catAnimator == null)
            return;
        catAnimator.enabled = true;
        catAnimator.Rebind();
        catAnimator.Update(0f);
        catAnimator.enabled = false;
    }

    // BoatBoarding calls this before changing the rigidbody state.  Keeping this
    // state here prevents any queued player input or cat animation from resuming.
    public void SetVehiclePassenger(bool passenger)
    {
        vehiclePassenger = passenger;
        movement = turn = 0f;
        jumpRequested = resetRequested = false;
        if (passenger)
            ResetCatToInitialPose();
    }

    private void Respawn()
    {
        rb.position = spawnPosition;
        rb.rotation = spawnRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        movement = turn = 0f;
        jumpRequested = resetRequested = false;
        isGrounded = false;
    }

    private void CreateCatVisual()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        GameObject kittyPrefab = Resources.Load<GameObject>("Kitty_001");
        if (kittyPrefab == null)
        {
            Debug.LogError("Kitty_001 prefab is missing from Assets/Resources.", this);
            return;
        }
        GameObject kitty = Instantiate(kittyPrefab, transform, false);
        kitty.name = "Kitty Player Visual";
        kitty.transform.localPosition = new Vector3(0f, .03f, 0f);
        kitty.transform.localRotation = Quaternion.identity;
        kitty.transform.localScale = Vector3.one * 3.5f;
        foreach (MonoBehaviour behaviour in kitty.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in kitty.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        catAnimator = kitty.GetComponentInChildren<Animator>(true);
        if (catAnimator == null)
            Debug.LogError("Kitty Player Visual has no Animator.", kitty);
        else
        {
            ResetCatToInitialPose();
            idleStartedAt = Time.unscaledTime;
        }
    }

    private void OnDestroy()
    {
        if (movementMaterial != null)
            Destroy(movementMaterial);
    }
}
