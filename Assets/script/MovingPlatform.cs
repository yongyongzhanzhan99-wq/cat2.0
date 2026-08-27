using UnityEngine;

/// <summary>
/// 让平台在两个点之间来回移动。
/// 挂在平台根物体上。
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    private Transform targetPoint;

    private void Start()
    {
        targetPoint = pointB;
    }

    private void Update()
    {
        if (pointA == null || pointB == null)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
        {
            targetPoint = targetPoint == pointA ? pointB : pointA;
        }
    }
}
