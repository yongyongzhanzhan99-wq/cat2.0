using UnityEngine;

namespace Controller
{
    [RequireComponent(typeof(CreatureMover))]
    public class MovePlayerInput : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField]
        private string m_HorizontalAxis = "Horizontal";

        [SerializeField]
        private string m_VerticalAxis = "Vertical";

        [SerializeField]
        private string m_JumpButton = "Jump";

        [SerializeField]
        private KeyCode m_RunKey = KeyCode.LeftShift;

        [Header("Camera")]
        [SerializeField]
        private PlayerCamera m_Camera;

        [SerializeField]
        private string m_MouseX = "Mouse X";

        [SerializeField]
        private string m_MouseY = "Mouse Y";

        [SerializeField]
        private string m_MouseScroll = "Mouse ScrollWheel";

        private CreatureMover m_Mover;

        private Vector2 m_Axis;
        private bool m_IsRun;
        private bool m_IsJump;

        private Vector3 m_Target;
        private Vector2 m_MouseDelta;
        private float m_Scroll;

        private void Awake()
        {
            m_Mover = GetComponent<CreatureMover>();
        }

        private void Update()
        {
            GatherInput();
            SetInput();
        }

        public void GatherInput()
        {
            m_Axis = new Vector2(
                Input.GetAxis(m_HorizontalAxis),
                Input.GetAxis(m_VerticalAxis)
            );

            m_IsRun = Input.GetKey(m_RunKey);

            // GetButtonDown 让跳跃只触发一次，而不是一直触发
            m_IsJump = Input.GetButtonDown(m_JumpButton);

            m_Target = (m_Camera == null)
                ? Vector3.zero
                : m_Camera.Target;

            m_MouseDelta = new Vector2(
                Input.GetAxis(m_MouseX),
                Input.GetAxis(m_MouseY)
            );

            m_Scroll = Input.GetAxis(m_MouseScroll);
        }

        public void BindMover(CreatureMover mover)
        {
            m_Mover = mover;
        }

        public void SetInput()
        {
            if (m_Mover != null)
            {
                m_Mover.SetInput(
                    in m_Axis,
                    in m_Target,
                    in m_IsRun,
                    in m_IsJump
                );
            }

            if (m_Camera != null)
            {
                m_Camera.SetInput(
                    in m_MouseDelta,
                    m_Scroll
                );
            }
        }
    }
}