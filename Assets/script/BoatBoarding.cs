using UnityEngine;

/// <summary>
/// 使用 CreatureMover 角色系统的上船/下船脚本。
/// 挂在船的 BoardZone 物体上。
/// </summary>
public class BoatBoarding : MonoBehaviour
{
    public Transform seatPoint;
    public Transform exitPoint;
    public BoatController boatController;

    private CharacterController characterController;
    private Controller.CreatureMover creatureMover;
    private Controller.MovePlayerInput movePlayerInput;
    private PlayerMove catPlayerMove;
    private Rigidbody catBody;
    private Animator passengerAnimator;
    private Transform playerTransform;
    private bool playerInside;
    private bool riding;
    private bool savedUseGravity;
    private bool savedKinematic;
    private bool savedDetectCollisions;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (!riding && playerInside && playerTransform != null)
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
        // Current Map2 player: Rigidbody + PlayerMove.
        PlayerMove map2Player = other.GetComponentInParent<PlayerMove>();
        if (map2Player != null)
        {
            catPlayerMove = map2Player;
            catBody = map2Player.GetComponent<Rigidbody>();
            passengerAnimator = map2Player.GetComponentInChildren<Animator>(true);
            playerTransform = map2Player.transform;
            playerInside = true;
            return;
        }

        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller == null)
            return;

        characterController = controller;
        playerTransform = controller.transform;
        creatureMover = controller.GetComponent<Controller.CreatureMover>();
        movePlayerInput = controller.GetComponent<Controller.MovePlayerInput>();
        passengerAnimator = controller.GetComponentInChildren<Animator>(true);
        playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMove map2Player = other.GetComponentInParent<PlayerMove>();
        if (!riding && map2Player != null && map2Player == catPlayerMove)
        {
            playerInside = false;
            return;
        }

        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (!riding && controller == characterController)
        {
            playerInside = false;
        }
    }

    private void GetOnBoat()
    {
        if (seatPoint == null || boatController == null)
            return;

        riding = true;
        playerInside = false;

        if (movePlayerInput != null)
            movePlayerInput.enabled = false;

        if (creatureMover != null)
            creatureMover.enabled = false;

        if (characterController != null)
            characterController.enabled = false;

        if (catPlayerMove != null)
        {
            catPlayerMove.SetVehiclePassenger(true);
            catPlayerMove.enabled = false;
        }

        SetPassengerAnimationEnabled(false);

        if (catBody != null)
        {
            savedUseGravity = catBody.useGravity;
            savedKinematic = catBody.isKinematic;
            savedDetectCollisions = catBody.detectCollisions;
            catBody.velocity = Vector3.zero;
            catBody.angularVelocity = Vector3.zero;
            catBody.useGravity = false;
            catBody.isKinematic = true;
            catBody.detectCollisions = false;
        }

        playerTransform.SetParent(seatPoint, false);
        playerTransform.localPosition = Vector3.zero;
        // The seat supplies the position; the cat's forward is matched to the boat.
        playerTransform.rotation = Quaternion.Euler(0f, boatController.transform.eulerAngles.y, 0f);

        boatController.isDriving = true;
    }

    private void GetOffBoat()
    {
        if (exitPoint == null || boatController == null)
            return;

        riding = false;
        boatController.isDriving = false;

        playerTransform.SetParent(null, true);
        playerTransform.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);

        if (characterController != null)
            characterController.enabled = true;

        if (creatureMover != null)
            creatureMover.enabled = true;

        if (movePlayerInput != null)
            movePlayerInput.enabled = true;

        if (catBody != null)
        {
            catBody.isKinematic = savedKinematic;
            catBody.useGravity = savedUseGravity;
            catBody.detectCollisions = savedDetectCollisions;
            catBody.velocity = Vector3.zero;
            catBody.angularVelocity = Vector3.zero;
        }

        if (catPlayerMove != null)
        {
            catPlayerMove.enabled = true;
            catPlayerMove.SetVehiclePassenger(false);
        }

        SetPassengerAnimationEnabled(true);

        characterController = null;
        creatureMover = null;
        movePlayerInput = null;
        catPlayerMove = null;
        catBody = null;
        passengerAnimator = null;
        playerTransform = null;
    }

    private void SetPassengerAnimationEnabled(bool enabled)
    {
        if (passengerAnimator == null)
            return;

        if (!enabled)
        {
            passengerAnimator.enabled = true;
            passengerAnimator.Rebind();
            passengerAnimator.Update(0f);
        }

        passengerAnimator.enabled = enabled;
    }
}
