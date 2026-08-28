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
        public event System.Action<AutoFruitPickup> Collected;
        public bool IsCollected { get; private set; }

        public void Configure(Transform target, Vector3 worldFruitCenter, float radius)
        {
            player = target;
            playerBody = player != null ? player.GetComponent<BoxCollider>() : null;
            localPickupPoint = transform.InverseTransformPoint(worldFruitCenter);
            pickupRadius = Mathf.Max(0f, radius);
        }

        private void Awake()
        {
            playerBody = player != null ? player.GetComponent<BoxCollider>() : null;
        }

        private void Update()
        {
            if (IsCollected || player == null || !player.gameObject.activeInHierarchy) return;
            // Measure from the body center, not the feet: a 2m-high fruit must be reachable during a jump.
            Vector3 playerCenter = playerBody != null ? playerBody.bounds.center : player.position;
            Vector3 offset = playerCenter - transform.TransformPoint(localPickupPoint);
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
