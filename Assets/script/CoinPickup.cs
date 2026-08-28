using UnityEngine;

/// <summary>
/// 角色靠近金币后，金币消失，模拟拾取效果。
/// 挂在金币物体上使用。
/// </summary>
public class CoinPickup : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 100f;

    private void Update()
    {
        // 让金币持续旋转，方便玩家发现。
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 通过 CharacterController 找到猫角色，兼容模型子物体上的碰撞体。
        CharacterController player = other.GetComponentInParent<CharacterController>();

        if (player == null)
        {
            return;
        }

        // 暂时用“消失”模拟拾取，不做背包和计数。
        gameObject.SetActive(false);
    }
}
