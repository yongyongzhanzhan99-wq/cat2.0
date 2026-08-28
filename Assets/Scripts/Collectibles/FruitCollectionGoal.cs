using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatGame
{
    [DisallowMultipleComponent]
    public sealed class FruitCollectionGoal : MonoBehaviour
    {
        [SerializeField] private AutoFruitPickup[] fruits;
        [Tooltip("Full scene asset path, for example Assets/Scenes/Map2.unity. Must be enabled in Build Settings.")]
        [SerializeField] private string nextScenePath = "";
        [SerializeField, Min(0f)] private float transitionDelay = .75f;
        private readonly HashSet<AutoFruitPickup> targets = new HashSet<AutoFruitPickup>();
        private readonly HashSet<AutoFruitPickup> collected = new HashSet<AutoFruitPickup>();
        private bool complete;
        private bool valid;
        private string message;

        public void Configure(AutoFruitPickup[] items) { fruits = items; }

        private void OnEnable()
        {
            targets.Clear(); collected.Clear(); complete = false; valid = true; message = null;
            if (fruits == null || fruits.Length == 0) valid = false;
            else foreach (var fruit in fruits)
            {
                if (fruit == null || fruit.gameObject.scene != gameObject.scene) { valid = false; continue; }
                if (!targets.Add(fruit)) continue;
                fruit.Collected += OnCollected;
                if (fruit.IsCollected) collected.Add(fruit);
            }
            if (!valid) Debug.LogError("Fruit goal has missing/invalid fruit references; automatic transition disabled.", this);
            TryComplete();
        }

        private void OnDisable()
        {
            foreach (var fruit in targets) if (fruit != null) fruit.Collected -= OnCollected;
            StopAllCoroutines();
        }

        private void OnCollected(AutoFruitPickup fruit)
        {
            // Disabling or destroying a fruit externally is not a collection event.
            if (!targets.Contains(fruit) || !fruit.IsCollected || !collected.Add(fruit)) return;
            TryComplete();
        }

        private void TryComplete()
        {
            if (!valid || complete || targets.Count == 0 || collected.Count != targets.Count) return;
            complete = true;
            int index = string.IsNullOrWhiteSpace(nextScenePath) ? -1 : SceneUtility.GetBuildIndexByScenePath(nextScenePath);
            if (index < 0 || nextScenePath == gameObject.scene.path || !Application.CanStreamedLevelBeLoaded(index))
            {
                message = "All fruits collected! Next scene is not configured.";
                Debug.LogWarning("Fruit goal complete, but select a different next scene and enable it in Build Settings. Staying in this scene.", this);
                return;
            }
            message = "All fruits collected! Loading next scene...";
            StartCoroutine(LoadNext(index));
        }

        private IEnumerator LoadNext(int index)
        {
            yield return new WaitForSecondsRealtime(transitionDelay);
            var operation = SceneManager.LoadSceneAsync(index, LoadSceneMode.Single);
            if (operation == null) { message = "Could not load next scene."; yield break; }
            yield return operation;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 76, complete ? 490 : 240, 50),
                "Fruits: " + collected.Count + " / " + targets.Count + "\n" + (message ?? "Collect every fruit to continue."));
        }
    }
}
