using UnityEngine;

public class BoatController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 60f;
    public bool isDriving = false;

    void Update()
    {
        if (!isDriving)
            return;

        float forward = Input.GetAxisRaw("Vertical");
        float turn = Input.GetAxisRaw("Horizontal");

        // 前后移动
        Vector3 movement = transform.forward * forward * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // 左右转向
        transform.Rotate(
            0f,
            turn * turnSpeed * Time.deltaTime,
            0f
        );
    }
}