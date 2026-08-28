using UnityEngine;

/// <summary>
/// 用 WASD 控制船前进、后退和转向。
/// 挂在船的根物体上。
/// </summary>
public class BoatController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 60f;
    public bool isDriving;

    private void Update()
    {
        if (!isDriving)
            return;

        float forward = Input.GetAxisRaw("Vertical");
        float turn = Input.GetAxisRaw("Horizontal");

        transform.position +=
            transform.forward * forward * moveSpeed * Time.deltaTime;

        transform.Rotate(
            0f,
            turn * turnSpeed * Time.deltaTime,
            0f
        );
    }
}
