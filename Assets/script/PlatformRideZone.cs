using UnityEngine;

/// <summary>
/// 角色进入平台触发区后，跟随平台移动。
/// 挂在平台的 RideZone 子物体上。
/// </summary>
public class PlatformRideZone : MonoBehaviour
{
    public Transform platform;

    private PlayerMove playerMove;
    private Rigidbody playerRigidbody;

    private void OnTriggerEnter(Collider other)
    {
        PlayerMove move = other.GetComponentInParent<PlayerMove>();

        if (move == null)
            return;

        playerMove = move;
        playerRigidbody = move.GetComponent<Rigidbody>();

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        playerMove.transform.SetParent(platform, true);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMove move = other.GetComponentInParent<PlayerMove>();

        if (move == null || move != playerMove)
            return;

        playerMove.transform.SetParent(null, true);

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
        }

        playerMove = null;
        playerRigidbody = null;
    }
}
