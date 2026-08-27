using System;
using UnityEngine;

namespace CityPeople
{
    [RequireComponent(typeof(Animator))]
    public class CityPeopleMovement : MonoBehaviour
    {
        public enum MotionMode
        {
            RootMotion,
            ScriptedSpeed
        }

        [Header("Movement mode")]
        [SerializeField]
        [Tooltip("优先用 Root Motion；若动画是原地踏步，改成 Scripted Speed。")]
        private MotionMode motionMode = MotionMode.RootMotion;

        [Header("Speed for Scripted Speed mode")]
        [SerializeField] private float slowWalkSpeed = 0.8f;
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float joggingSpeed = 2.4f;
        [SerializeField] private float runningSpeed = 4.2f;

        [Header("Direction")]
        [SerializeField]
        [Tooltip("模型 Z 轴朝前时保持开启；若人物倒着走，取消勾选。")]
        private bool moveForward = true;

        [Header("Character / Animal Avoidance")]
        [SerializeField]
        [Tooltip("开始检测其他角色的距离。")]
        private float detectionDistance = 0.9f;

        [SerializeField]
        [Tooltip("前方检测球的半径。")]
        private float detectionRadius = 0.28f;

        [SerializeField]
        [Tooltip("角色转身速度，数值越小转得越慢。")]
        private float turnSpeed = 180f;

        [SerializeField]
        [Tooltip("检测起点高度，通常放在人物腰部附近。")]
        private float detectionHeight = 0.8f;

        [SerializeField]
        [Tooltip("Animals_FREE 角色使用的 Tag。请在 Unity 中给动物根物体设置这个 Tag。")]
        private string animalTag = "Animal";

        [Header("Character Waiting")]
        [SerializeField]
        [Tooltip("玩家或其他需要让行的角色 Tag。检测到后 CityPeople 会停下等待，不会转向。")]
        private string characterTag = "Character";

        [SerializeField]
        [Tooltip("等待中的 Character 离开这个距离后，CityPeople 才恢复移动。建议略大于 Detection Distance。")]
        private float characterReleaseDistance = 1.2f;

        private Animator animator;
        private bool isLocomotion;
        private float currentSpeed;
        private string currentClipName;

        // 避让状态
        private bool isTurningAway;
        private Quaternion targetRotation;

        // 遇到 Character 后的等待状态。
        private bool isWaitingForCharacter;
        private Transform waitingCharacter;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            // 不使用 Animator 自动应用 Root Motion，
            // 由 OnAnimatorMove 控制，确保只有 locom 动画才会产生位移。
            animator.applyRootMotion = false;
        }

        /// <summary>
        /// 由 CityPeople 在切换动画时调用。
        /// </summary>
        public void SetMovementForClip(AnimationClip clip)
        {
            if (clip == null)
            {
                StopMovement();
                return;
            }

            currentClipName = clip.name;
            string clipNameLower = currentClipName.ToLowerInvariant();

            // locom_f_* 与 locom_m_* 都属于移动动画。
            isLocomotion = clipNameLower.StartsWith("locom_");

            if (!isLocomotion)
            {
                currentSpeed = 0f;
                isTurningAway = false;
                return;
            }

            if (clipNameLower.Contains("running"))
            {
                currentSpeed = runningSpeed;
            }
            else if (clipNameLower.Contains("jogging"))
            {
                currentSpeed = joggingSpeed;
            }
            else if (clipNameLower.Contains("slowwalk"))
            {
                currentSpeed = slowWalkSpeed;
            }
            else
            {
                // basicWalk、phoneWalking 默认按普通走路速度移动。
                currentSpeed = walkSpeed;
            }
        }

        private void Update()
        {
            if (!isLocomotion)
                return;

            // 如果之前遇到了 Character，就一直原地等待，直到它真正离开。
            if (isWaitingForCharacter)
            {
                if (ShouldKeepWaitingForCharacter())
                    return;

                isWaitingForCharacter = false;
                waitingCharacter = null;
            }

            // Character 的优先级最高：检测到后只停车等待，不转身、不绕开。
            if (TryFindTaggedCharacterAhead(out Transform taggedCharacter))
            {
                waitingCharacter = taggedCharacter;
                isWaitingForCharacter = true;
                isTurningAway = false;
                return;
            }

            // 正在避让普通 CityPeople / Animal 时：原地转身，不向前移动。
            if (isTurningAway)
            {
                RotateAway();
                return;
            }

            // 前方检测到其他 CityPeople 或 Animal：先停下，然后开始转向。
            if (TryFindCharacterAhead(out Transform otherCharacter))
            {
                BeginTurnAway(otherCharacter);
                return;
            }

            if (motionMode != MotionMode.ScriptedSpeed)
                return;

            Vector3 direction = moveForward ? transform.forward : -transform.forward;
            transform.position += direction * currentSpeed * Time.deltaTime;
        }

        private void OnAnimatorMove()
        {
            if (motionMode != MotionMode.RootMotion || !isLocomotion)
                return;

            // 等待 Character 或避让过程中都禁止 Root Motion 位移。
            // 等待时保持当前朝向；避让时转向只由 RotateAway 控制。
            if (isWaitingForCharacter || isTurningAway)
                return;

            // 播放 locom 动画时应用动画自身的位移与旋转。
            transform.position += animator.deltaPosition;
            transform.rotation *= animator.deltaRotation;
        }


        /// <summary>
        /// 检测移动方向前方是否有带 Character Tag 的对象。
        /// Character 的优先级高于普通 CityPeople / Animal 避让。
        /// </summary>
        private bool TryFindTaggedCharacterAhead(out Transform character)
        {
            character = null;

            if (string.IsNullOrEmpty(characterTag))
                return false;

            Vector3 moveDirection = moveForward ? transform.forward : -transform.forward;
            Vector3 origin = transform.position + Vector3.up * detectionHeight;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                detectionRadius,
                moveDirection,
                detectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                Transform current = hit.collider.transform;
                Transform taggedRoot = null;

                // Tag 可能挂在碰撞体自身，也可能挂在它的父物体。
                while (current != null)
                {
                    if (current.CompareTag(characterTag))
                    {
                        taggedRoot = current;
                        break;
                    }

                    current = current.parent;
                }

                if (taggedRoot == null)
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    character = taggedRoot;
                }
            }

            return character != null;
        }

        /// <summary>
        /// 已经停车后，不再要求 Character 必须一直位于正前方。
        /// 只要它还在等待距离内，就继续原地等待。
        /// </summary>
        private bool ShouldKeepWaitingForCharacter()
        {
            if (waitingCharacter == null)
                return false;

            Vector3 offset = waitingCharacter.position - transform.position;
            offset.y = 0f;

            return offset.sqrMagnitude <= characterReleaseDistance * characterReleaseDistance;
        }

        /// <summary>
        /// 检测移动方向前方是否有另一个 CityPeople 或 Animals_FREE 动物。
        /// </summary>
        private bool TryFindCharacterAhead(out Transform otherCharacter)
        {
            otherCharacter = null;

            Vector3 moveDirection = moveForward ? transform.forward : -transform.forward;
            Vector3 origin = transform.position + Vector3.up * detectionHeight;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                detectionRadius,
                moveDirection,
                detectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                // 忽略自己的 Collider 或自己的子物体 Collider。
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                // 1. CityPeople：直接通过组件识别。
                CityPeople otherPerson = hit.collider.GetComponentInParent<CityPeople>();
                Transform detectedTarget = null;

                if (otherPerson != null && otherPerson.gameObject != gameObject)
                {
                    detectedTarget = otherPerson.transform;
                }
                else
                {
                    // 2. Animals_FREE：通过 Animal Tag 识别。
                    // Tag 可以放在动物根物体，也可以放在碰撞体所在物体。
                    Transform current = hit.collider.transform;

                    while (current != null)
                    {
                        if (!string.IsNullOrEmpty(animalTag) && current.CompareTag(animalTag))
                        {
                            detectedTarget = current;
                            break;
                        }

                        current = current.parent;
                    }
                }

                if (detectedTarget == null)
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    otherCharacter = detectedTarget;
                }
            }

            return otherCharacter != null;
        }

        /// <summary>
        /// 碰到角色后，计算一个背离对方的方向。
        /// </summary>
        private void BeginTurnAway(Transform otherCharacter)
        {
            Vector3 awayDirection = transform.position - otherCharacter.position;
            awayDirection.y = 0f;

            // 两个角色几乎完全重合时，直接掉头。
            if (awayDirection.sqrMagnitude < 0.001f)
            {
                awayDirection = moveForward ? -transform.forward : transform.forward;
            }

            awayDirection.Normalize();

            // moveForward=false 时模型实际移动方向与 transform.forward 相反，
            // 因此目标朝向也要反过来。
            Vector3 modelForward = moveForward ? awayDirection : -awayDirection;

            targetRotation = Quaternion.LookRotation(modelForward, Vector3.up);
            isTurningAway = true;
        }

        /// <summary>
        /// 原地平滑转身。转完后恢复正常移动。
        /// </summary>
        private void RotateAway()
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(transform.rotation, targetRotation) <= 1f)
            {
                transform.rotation = targetRotation;
                isTurningAway = false;
            }
        }

        public void StopMovement()
        {
            isLocomotion = false;
            currentSpeed = 0f;
            currentClipName = string.Empty;
            isTurningAway = false;
            isWaitingForCharacter = false;
            waitingCharacter = null;
        }

        public string GetCurrentClipName()
        {
            return currentClipName;
        }

        // 在 Scene 视图里显示检测范围，方便调整参数。
        private void OnDrawGizmosSelected()
        {
            Vector3 direction = moveForward ? transform.forward : -transform.forward;
            Vector3 origin = transform.position + Vector3.up * detectionHeight;

            Gizmos.DrawWireSphere(origin, detectionRadius);
            Gizmos.DrawWireSphere(origin + direction * detectionDistance, detectionRadius);
            Gizmos.DrawLine(origin, origin + direction * detectionDistance);
        }
    }
}
