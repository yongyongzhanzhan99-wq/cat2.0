using System;
using UnityEngine;

namespace Controller
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class CreatureMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField]
        private float m_WalkSpeed = 1f;

        [SerializeField]
        private float m_RunSpeed = 4f;

        [SerializeField, Range(0f, 360f)]
        private float m_RotateSpeed = 40f;

        [SerializeField]
        private Space m_Space = Space.Self;

        [Header("Camera")]
        [Tooltip("用于决定移动方向的摄像机。为空时自动使用 Camera.main")]
        [SerializeField]
        private Transform m_Camera;

        [SerializeField]
        private float m_JumpHeight = 5f;

        [Header("Animator")]
        [SerializeField]
        private string m_VerticalID = "Vert";

        [SerializeField]
        private string m_StateID = "State";

        [SerializeField]
        private LookWeight m_LookWeight =
            new LookWeight(1f, 0.3f, 0.7f, 1f);

        private Transform m_Transform;
        private CharacterController m_Controller;
        private Animator m_Animator;

        private MovementHandler m_Movement;
        private AnimationHandler m_Animation;

        private Vector2 m_Axis;
        private Vector3 m_Target;

        private bool m_IsRun;
        private bool m_LastRunInput;
        private bool m_IsMoving;

        public Vector2 Axis => m_Axis;
        public Vector3 Target => m_Target;
        public bool IsRun => m_IsRun;

        private void OnValidate()
        {
            m_WalkSpeed = Mathf.Max(m_WalkSpeed, 0f);
            m_RunSpeed = Mathf.Max(m_RunSpeed, m_WalkSpeed);
            m_JumpHeight = Mathf.Max(m_JumpHeight, 0f);

            m_Movement?.SetStats(
                m_WalkSpeed / 3.6f,
                m_RunSpeed / 3.6f,
                m_RotateSpeed,
                m_JumpHeight,
                m_Space
            );
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            m_Transform = transform;

            if (m_Controller == null)
            {
                m_Controller =
                    GetComponent<CharacterController>();
            }

            if (m_Animator == null)
            {
                m_Animator =
                    GetComponent<Animator>();
            }

            if (m_Camera == null)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                {
                    m_Camera = mainCamera.transform;
                }
            }

            if (m_Controller == null)
            {
                Debug.LogError(
                    "CreatureMover: 找不到 CharacterController。",
                    this
                );

                return;
            }

            if (m_Animator == null)
            {
                Debug.LogError(
                    "CreatureMover: 找不到 Animator。",
                    this
                );

                return;
            }

            m_Movement = new MovementHandler(
                m_Controller,
                m_Transform,
                m_Camera,
                m_WalkSpeed,
                m_RunSpeed,
                m_RotateSpeed,
                m_JumpHeight,
                m_Space
            );

            m_Animation = new AnimationHandler(
                m_Animator,
                m_VerticalID,
                m_StateID
            );
        }

        private void Update()
        {
            // 防止 Unity 运行时重新编译脚本后
            // m_Movement / m_Animation 丢失导致 NullReferenceException。
            if (m_Movement == null ||
                m_Animation == null)
            {
                Initialize();

                if (m_Movement == null ||
                    m_Animation == null)
                {
                    return;
                }
            }

            m_Movement.Move(
                Time.deltaTime,
                in m_Axis,
                in m_Target,
                m_IsRun,
                m_IsMoving,
                out var animAxis,
                out var isAir
            );

            m_Animation.Animate(
                in animAxis,
                m_IsRun ? 1f : 0f,
                Time.deltaTime
            );
        }

        private void OnAnimatorIK()
        {
            if (m_Animation == null)
            {
                return;
            }

            m_Animation.AnimateIK(
                in m_Target,
                m_LookWeight
            );
        }

        public void SetInput(
            in Vector2 axis,
            in Vector3 target,
            in bool isRun,
            in bool isJump)
        {
            m_Axis = axis;
            m_Target = target;

            // Shift 点按切换：
            //
            // 第一次按下 Shift -> 奔跑
            // 再按一次 Shift   -> 走路
            //
            // 按住 Shift 不会重复切换。
            if (isRun && !m_LastRunInput)
            {
                m_IsRun = !m_IsRun;
            }

            m_LastRunInput = isRun;

            if (m_Axis.sqrMagnitude <
                Mathf.Epsilon)
            {
                m_Axis = Vector2.zero;
                m_IsMoving = false;
            }
            else
            {
                m_Axis =
                    Vector2.ClampMagnitude(
                        m_Axis,
                        1f
                    );

                m_IsMoving = true;
            }

            if (isJump)
            {
                if (m_Movement == null)
                {
                    Initialize();
                }

                m_Movement?.Jump();
            }
        }

        private void OnControllerColliderHit(
            ControllerColliderHit hit)
        {
            if (m_Controller == null ||
                m_Movement == null)
            {
                return;
            }

            if (hit.normal.y >
                m_Controller.stepOffset)
            {
                m_Movement.SetSurface(
                    hit.normal
                );
            }
        }

        [Serializable]
        private struct LookWeight
        {
            public float weight;
            public float body;
            public float head;
            public float eyes;

            public LookWeight(
                float weight,
                float body,
                float head,
                float eyes)
            {
                this.weight = weight;
                this.body = body;
                this.head = head;
                this.eyes = eyes;
            }
        }

        #region Handlers

        private class MovementHandler
        {
            private readonly CharacterController m_Controller;
            private readonly Transform m_Transform;
            private readonly Transform m_Camera;

            private float m_WalkSpeed;
            private float m_RunSpeed;
            private float m_RotateSpeed;
            private float m_JumpHeight;

            private Space m_Space;

            private readonly float m_Luft = 75f;

            // 移动时的小角度死区。
            // 防止角色为了几度偏差不停修正方向。
            private readonly float m_MoveRotateDeadZone = 8f;

            private float m_TargetAngle;
            private bool m_IsRotating = false;

            private Vector3 m_Normal =
                Vector3.up;

            private Vector3 m_GravityAcelleration =
                Physics.gravity;

            private float m_JumpTimer;

            public MovementHandler(
                CharacterController controller,
                Transform transform,
                Transform camera,
                float walkSpeed,
                float runSpeed,
                float rotateSpeed,
                float jumpHeight,
                Space space)
            {
                m_Controller = controller;
                m_Transform = transform;
                m_Camera = camera;

                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
                m_JumpHeight = jumpHeight;

                m_Space = space;
            }

            public void SetStats(
                float walkSpeed,
                float runSpeed,
                float rotateSpeed,
                float jumpHeight,
                Space space)
            {
                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
                m_JumpHeight = jumpHeight;

                m_Space = space;
            }

            public void SetSurface(
                in Vector3 normal)
            {
                m_Normal = normal;
            }

            public void Jump()
            {
                if (!m_Controller.isGrounded)
                {
                    return;
                }

                if (m_JumpTimer > 0f)
                {
                    return;
                }

                float jumpVelocity =
                    Mathf.Sqrt(
                        m_JumpHeight *
                        -2f *
                        Physics.gravity.y
                    );

                m_GravityAcelleration.y =
                    jumpVelocity;

                m_JumpTimer = 0.1f;
            }

            public void Move(
                float deltaTime,
                in Vector2 axis,
                in Vector3 target,
                bool isRun,
                bool isMoving,
                out Vector2 animAxis,
                out bool isAir)
            {
                Vector3 cameraForward;

                if (m_Camera != null)
                {
                    cameraForward =
                        Vector3.ProjectOnPlane(
                            m_Camera.forward,
                            Vector3.up
                        ).normalized;
                }
                else
                {
                    cameraForward =
                        Vector3.ProjectOnPlane(
                            target -
                            m_Transform.position,
                            Vector3.up
                        ).normalized;
                }

                // 防止镜头完全垂直向上/向下时
                // 投影结果变成零向量。
                if (cameraForward.sqrMagnitude <
                    Mathf.Epsilon)
                {
                    cameraForward =
                        Vector3.ProjectOnPlane(
                            m_Transform.forward,
                            Vector3.up
                        ).normalized;
                }

                ConvertMovement(
                    in axis,
                    in cameraForward,
                    out var movement
                );

                Vector3 targetForward;

                if (movement.sqrMagnitude >
                    Mathf.Epsilon)
                {
                    targetForward =
                        movement.normalized;
                }
                else
                {
                    targetForward =
                        m_Transform.forward;
                }

                CaculateGravity(
                    deltaTime,
                    out isAir
                );

                Displace(
                    deltaTime,
                    in movement,
                    isRun
                );

                Turn(
                    in targetForward,
                    isMoving
                );

                UpdateRotation(
                    deltaTime
                );

                GenAnimationAxis(
                    in movement,
                    out animAxis
                );
            }

            private void ConvertMovement(
                in Vector2 axis,
                in Vector3 targetForward,
                out Vector3 movement)
            {
                Vector3 forward;
                Vector3 right;

                if (m_Space == Space.Self)
                {
                    forward =
                        new Vector3(
                            targetForward.x,
                            0f,
                            targetForward.z
                        ).normalized;

                    right =
                        Vector3.Cross(
                            Vector3.up,
                            forward
                        ).normalized;
                }
                else
                {
                    forward =
                        Vector3.forward;

                    right =
                        Vector3.right;
                }

                movement =
                    axis.x * right +
                    axis.y * forward;

                movement =
                    Vector3.ProjectOnPlane(
                        movement,
                        m_Normal
                    );
            }

            private void Displace(
                float deltaTime,
                in Vector3 movement,
                bool isRun)
            {
                Vector3 displacement =
                    (isRun
                        ? m_RunSpeed
                        : m_WalkSpeed)
                    * movement;

                displacement +=
                    m_GravityAcelleration;

                displacement *=
                    deltaTime;

                m_Controller.Move(
                    displacement
                );
            }

            private void CaculateGravity(
                float deltaTime,
                out bool isAir)
            {
                m_JumpTimer =
                    Mathf.Max(
                        m_JumpTimer -
                        deltaTime,
                        0f
                    );

                if (m_Controller.isGrounded)
                {
                    if (m_GravityAcelleration.y < 0f)
                    {
                        m_GravityAcelleration.y =
                            Physics.gravity.y;
                    }

                    isAir = false;

                    return;
                }

                isAir = true;

                m_GravityAcelleration +=
                    Physics.gravity *
                    deltaTime;
            }

            private void GenAnimationAxis(
                in Vector3 movement,
                out Vector2 animAxis)
            {
                if (m_Space == Space.Self)
                {
                    animAxis =
                        new Vector2(
                            Vector3.Dot(
                                movement,
                                m_Transform.right
                            ),
                            Vector3.Dot(
                                movement,
                                m_Transform.forward
                            )
                        );
                }
                else
                {
                    animAxis =
                        new Vector2(
                            Vector3.Dot(
                                movement,
                                Vector3.right
                            ),
                            Vector3.Dot(
                                movement,
                                Vector3.forward
                            )
                        );
                }
            }

            private void Turn(
                in Vector3 targetForward,
                bool isMoving)
            {
                Vector3 flatTarget =
                    Vector3.ProjectOnPlane(
                        targetForward,
                        Vector3.up
                    );

                if (flatTarget.sqrMagnitude <
                    Mathf.Epsilon)
                {
                    return;
                }

                float angle =
                    Vector3.SignedAngle(
                        m_Transform.forward,
                        flatTarget,
                        Vector3.up
                    );

                // 移动过程中角度误差小于 8 度
                // 就不再继续微调。
                if (isMoving &&
                    Mathf.Abs(angle) <
                    m_MoveRotateDeadZone)
                {
                    m_IsRotating = false;
                    m_TargetAngle = 0f;

                    return;
                }

                if (!m_IsRotating)
                {
                    if (!isMoving &&
                        Mathf.Abs(angle) <
                        m_Luft)
                    {
                        m_IsRotating = false;

                        return;
                    }

                    m_IsRotating = true;
                }

                m_TargetAngle = angle;
            }

            private void UpdateRotation(
                float deltaTime)
            {
                if (!m_IsRotating)
                {
                    return;
                }

                float rotDelta =
                    m_RotateSpeed *
                    deltaTime;

                if (rotDelta +
                    Mathf.Epsilon >=
                    Mathf.Abs(m_TargetAngle))
                {
                    rotDelta =
                        m_TargetAngle;

                    m_IsRotating = false;
                }
                else
                {
                    rotDelta *=
                        Mathf.Sign(
                            m_TargetAngle
                        );
                }

                m_Transform.Rotate(
                    Vector3.up,
                    rotDelta
                );
            }
        }

        private class AnimationHandler
        {
            private readonly Animator m_Animator;
            private readonly string m_VerticalID;
            private readonly string m_StateID;

            private readonly float k_InputFlow =
                4.5f;

            private float m_FlowState;
            private Vector2 m_FlowAxis;

            public AnimationHandler(
                Animator animator,
                string verticalID,
                string stateID)
            {
                m_Animator = animator;
                m_VerticalID = verticalID;
                m_StateID = stateID;
            }

            public void Animate(
                in Vector2 axis,
                float state,
                float deltaTime)
            {
                m_Animator.SetFloat(
                    m_VerticalID,
                    m_FlowAxis.magnitude
                );

                m_Animator.SetFloat(
                    m_StateID,
                    Mathf.Clamp01(
                        m_FlowState
                    )
                );

                if ((axis - m_FlowAxis)
                        .sqrMagnitude >
                    Mathf.Epsilon)
                {
                    m_FlowAxis =
                        Vector2.ClampMagnitude(
                            m_FlowAxis +
                            k_InputFlow *
                            deltaTime *
                            (axis -
                             m_FlowAxis)
                            .normalized,
                            1f
                        );
                }

                m_FlowState =
                    Mathf.Clamp01(
                        m_FlowState +
                        k_InputFlow *
                        deltaTime *
                        Mathf.Sign(
                            state -
                            m_FlowState
                        )
                    );
            }

            public void AnimateIK(
                in Vector3 target,
                in LookWeight lookWeight)
            {
                if (m_Animator == null)
                {
                    return;
                }

                m_Animator.SetLookAtPosition(
                    target
                );

                m_Animator.SetLookAtWeight(
                    lookWeight.weight,
                    lookWeight.body,
                    lookWeight.head,
                    lookWeight.eyes
                );
            }
        }

        #endregion
    }
}