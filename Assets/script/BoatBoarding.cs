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
    private Transform playerTransform;
    private bool playerInside;
    private bool riding;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (!riding && playerInside && characterController != null)
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
        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller == null)
            return;

        characterController = controller;
        playerTransform = controller.transform;
        creatureMover = controller.GetComponent<Controller.CreatureMover>();
        movePlayerInput = controller.GetComponent<Controller.MovePlayerInput>();
        playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
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

        characterController.enabled = false;
        playerTransform.SetParent(seatPoint, false);
        playerTransform.localPosition = Vector3.zero;
        playerTransform.localRotation = Quaternion.identity;

        boatController.isDriving = true;
    }

    private void GetOffBoat()
    {
        if (exitPoint == null || boatController == null)
            return;

        riding = false;
        boatController.isDriving = false;

        playerTransform.SetParent(null, true);
        playerTransform.position = exitPoint.position;

        characterController.enabled = true;

        if (creatureMover != null)
            creatureMover.enabled = true;

        if (movePlayerInput != null)
            movePlayerInput.enabled = true;

        characterController = null;
        creatureMover = null;
        movePlayerInput = null;
        playerTransform = null;
    }
}
