using UnityEngine;

namespace CatGame
{
    [DisallowMultipleComponent]
    public sealed class AutoFruitPickup : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float pickupRadius = .75f;
        [SerializeField] private Vector3 localPickupPoint;
        private Collider playerBody;
        private CubeFirstPersonController playerMotor;
        public event System.Action<AutoFruitPickup> Collected;
        public bool IsCollected { get; private set; }

        public void Configure(Transform target, Vector3 worldFruitCenter, float radius)
        {
            player = target;
            playerBody = player != null ? player.GetComponent<BoxCollider>() : null;
            playerMotor = player != null ? player.GetComponent<CubeFirstPersonController>() : null;
            localPickupPoint = transform.InverseTransformPoint(worldFruitCenter);
            pickupRadius = Mathf.Max(0f, radius);
        }

        private void Awake()
        {
            playerBody = player != null ? player.GetComponent<BoxCollider>() : null;
            playerMotor = player != null ? player.GetComponent<CubeFirstPersonController>() : null;
            if (playerBody == null || playerMotor == null)
                Debug.LogError("Fruit pickup requires the Rigidbody cube player's BoxCollider and movement script.", this);
        }

        private void Update()
        {
            if (IsCollected || player == null || !player.gameObject.activeInHierarchy) return;
            if (playerBody == null || !playerBody.enabled || playerMotor == null || !playerMotor.enabled) return;
            // Keep jump-only collection, but accept contact with any part of the body.
            if (playerMotor.IsGrounded) return;
            Vector3 fruitCenter = transform.TransformPoint(localPickupPoint);
            Vector3 offset = playerBody.ClosestPoint(fruitCenter) - fruitCenter;
            if (IsWithinRadius(offset, pickupRadius))
            {
                IsCollected = true;
                gameObject.SetActive(false);
                Collected?.Invoke(this);
            }
        }

        // True three-dimensional distance; does not require a Rigidbody or trigger.
        public static bool IsWithinRadius(Vector3 offset, float radius)
        {
            return radius >= 0f && offset.sqrMagnitude <= radius * radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .8f, .1f, .65f);
            Gizmos.DrawWireSphere(transform.TransformPoint(localPickupPoint), pickupRadius);
        }
    }
}
