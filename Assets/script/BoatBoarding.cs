using UnityEngine;

public class BoatBoarding : MonoBehaviour
{
    public Transform seatPoint;
    public Transform exitPoint;
    public BoatController boatController;

    private PlayerMove playerMove;
    private Rigidbody playerRigidbody;
    private bool playerInside;
    private bool riding;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (!riding && playerInside && playerMove != null)
        {
            GetOnBoat();
        }
        else if (riding)
        {
            GetOffBoat();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMove move = other.GetComponentInParent<PlayerMove>();

        if (move != null)
        {
            playerMove = move;
            playerRigidbody = move.GetComponent<Rigidbody>();
            playerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!riding && other.GetComponentInParent<PlayerMove>() != null)
        {
            playerInside = false;
        }
    }

    private void GetOnBoat()
    {
        riding = true;
        playerInside = false;

        playerMove.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        playerMove.transform.SetParent(seatPoint);
        playerMove.transform.localPosition = Vector3.zero;
        playerMove.transform.localRotation = Quaternion.identity;

        boatController.isDriving = true;
    }

    private void GetOffBoat()
    {
        riding = false;

        boatController.isDriving = false;

        playerMove.transform.SetParent(null);
        playerMove.transform.position = exitPoint.position;

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
        }

        playerMove.enabled = true;
        playerMove = null;
    }
}