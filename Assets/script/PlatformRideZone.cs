using UnityEngine;

/// <summary>
/// 角色进入平台触发区后，跟随平台移动。
/// 挂在平台的 RideZone 子物体上。
/// </summary>
public class PlatformRideZone : MonoBehaviour
{
    public Transform platform;

    private CharacterController characterController;
    private Controller.CreatureMover creatureMover;
    private Controller.MovePlayerInput movePlayerInput;

    private void OnTriggerEnter(Collider other)
    {
        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller == null)
            return;

        characterController = controller;
        creatureMover = controller.GetComponent<Controller.CreatureMover>();
        movePlayerInput = controller.GetComponent<Controller.MovePlayerInput>();

        if (creatureMover != null)
            creatureMover.enabled = false;

        if (movePlayerInput != null)
            movePlayerInput.enabled = false;

        characterController.enabled = false;
        characterController.transform.SetParent(platform, true);
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller == null || controller != characterController)
            return;

        characterController.transform.SetParent(null, true);
        characterController.enabled = true;

        if (creatureMover != null)
            creatureMover.enabled = true;

        if (movePlayerInput != null)
            movePlayerInput.enabled = true;

        characterController = null;
        creatureMover = null;
        movePlayerInput = null;
    }
}
