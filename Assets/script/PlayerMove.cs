using UnityEngine;

/// <summary>
/// 挂在玩家根物体 character 上的简单移动脚本。
/// character 需要有 Rigidbody；玩家模型和 Camera 可以作为它的子物体。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float turnSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private int maxJumps = 2;

    private Rigidbody rb;
    private Vector3 inputDirection;
    private Transform viewTransform;
    private bool jumpRequested;
    private bool isGrounded;
    private int jumpsRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 优先使用 MainCamera；如果没有 MainCamera，就使用玩家自己的朝向。
        if (Camera.main != null)
        {
            viewTransform = Camera.main.transform;
        }

        // 锁住侧翻，只允许角色绕 Y 轴转向。
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        jumpsRemaining = maxJumps;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (Input.GetKeyDown(KeyCode.Space) && jumpsRemaining > 0)
        {
            jumpRequested = true;
            jumpsRemaining--;
        }
    }

    private void FixedUpdate()
    {
        if (inputDirection.sqrMagnitude >= 0.001f)
        {
            Transform reference = viewTransform != null ? viewTransform : transform;

            // 只使用摄像机的水平朝向，避免鼠标抬头/低头导致角色飞起来。
            Vector3 forward = reference.forward;
            Vector3 right = reference.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = (forward * inputDirection.z + right * inputDirection.x).normalized;
            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

            rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);

            // 只在地面上转身，跳跃过程中保持当前朝向。
            if (isGrounded)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                Quaternion smoothRotation = Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    turnSpeed * Time.fixedDeltaTime
                );
                rb.MoveRotation(smoothRotation);
            }
        }

        if (jumpRequested)
        {
            jumpRequested = false;
            isGrounded = false;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // 防止碰撞或斜坡让角色在空中旋转。
        if (!isGrounded)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                jumpsRemaining = maxJumps;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
