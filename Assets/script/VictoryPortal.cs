using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 角色进入黑洞终点后加载胜利场景。
/// 挂在黑洞物体或其触发区域上。
/// </summary>
public class VictoryPortal : MonoBehaviour
{
    public string victorySceneName = "Victory";

    private void OnTriggerEnter(Collider other)
    {
        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player != null)
        {
            SceneManager.LoadScene(victorySceneName);
        }
    }
}
