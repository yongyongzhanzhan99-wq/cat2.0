using UnityEngine;

namespace CatGame
{
    [DisallowMultipleComponent]
    public sealed class AutoFruitPickup : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float pickupRadius = 1.5f;
        [SerializeField] private Vector3 localPickupPoint;
        public event System.Action<AutoFruitPickup> Collected;
        public bool IsCollected { get; private set; }

        public void Configure(Transform target, Vector3 worldFruitCenter, float radius)
        {
            player = target;
            localPickupPoint = transform.InverseTransformPoint(worldFruitCenter);
            pickupRadius = Mathf.Max(0f, radius);
        }

        private void Update()
        {
            if (IsCollected || player == null || !player.gameObject.activeInHierarchy) return;
            Vector3 offset = player.position - transform.TransformPoint(localPickupPoint);
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
