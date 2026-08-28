using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatGame
{
    public sealed class CatStartMenu : MonoBehaviour
    {
        [SerializeField] private Button welcomeButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string targetScenePath="Assets/Scenes/Gamemap1.unity";
        private bool loading;
        public void Configure(Button button,TMP_Text label,TMP_Text status)
        { welcomeButton=button;buttonLabel=label;statusLabel=status; }
        private void Start()
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(EventSystem.current!=null&&welcomeButton!=null)EventSystem.current.SetSelectedGameObject(welcomeButton.gameObject);
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Return)&&Application.isFocused)StartGame();
        }
        public void StartGame()
        {
            if(loading)return;
            int index=SceneUtility.GetBuildIndexByScenePath(targetScenePath);
            if(index<0||!Application.CanStreamedLevelBeLoaded(index))
            {
                if(statusLabel!=null)statusLabel.text="Map1 is missing from Build Settings.";
                Debug.LogError("Enable "+targetScenePath+" in Build Settings.",this);return;
            }
            loading=true;if(welcomeButton!=null)welcomeButton.interactable=false;
            if(buttonLabel!=null)buttonLabel.text="Loading...";
            if(statusLabel!=null)statusLabel.text="Loading the village...";
            Time.timeScale=1;StartCoroutine(Load(index));
        }
        private IEnumerator Load(int index)
        {
            var operation=SceneManager.LoadSceneAsync(index,LoadSceneMode.Single);
            if(operation==null)
            {
                loading=false;if(welcomeButton!=null)welcomeButton.interactable=true;
                if(buttonLabel!=null)buttonLabel.text="Welcome";
                if(statusLabel!=null)statusLabel.text="Could not open Map1. Please try again.";yield break;
            }
            yield return operation;
        }
    }
}
