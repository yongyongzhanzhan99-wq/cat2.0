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
        private Texture2D map1FruitTexture;

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
            ApplyMap1FruitAppearance();
            TryComplete();
        }

        // Only Map1's configured pickup targets receive this visual override.
        // A property block keeps their source materials and every other scene unchanged.
        private void ApplyMap1FruitAppearance()
        {
            if (gameObject.scene.path != "Assets/Scenes/Gamemap1.unity")
                return;

            map1FruitTexture = Resources.Load<Texture2D>("Map1FruitTexture");
            if (map1FruitTexture == null)
            {
                Debug.LogError("Map1FruitTexture is missing from Assets/Resources.", this);
                return;
            }

            foreach (var fruit in targets)
            {
                if (fruit == null)
                    continue;

                foreach (var renderer in fruit.GetComponentsInChildren<Renderer>(true))
                {
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    properties.SetTexture("_MainTex", map1FruitTexture);
                    properties.SetTexture("_BaseMap", map1FruitTexture);
                    renderer.SetPropertyBlock(properties);
                }
            }
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
            if (string.IsNullOrWhiteSpace(nextScenePath) || nextScenePath == gameObject.scene.path)
            {
                message = "All fruits collected! Next scene is not configured.";
                Debug.LogWarning("Fruit goal complete, but select a different next scene and enable it in Build Settings. Staying in this scene.", this);
                return;
            }
            message = "All fruits collected! Loading next scene...";
            StartCoroutine(LoadNext(nextScenePath));
        }

        private IEnumerator LoadNext(string scenePath)
        {
            yield return new WaitForSecondsRealtime(transitionDelay);
            AsyncOperation operation;
            try
            {
                // This project has two scenes named "map2".  Resolve the exact
                // configured asset path so Unity cannot choose the legacy map.
                int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
                if (buildIndex < 0)
                {
                    message = "Could not load next scene.";
                    Debug.LogError("The next scene is not in Build Settings: " + scenePath, this);
                    yield break;
                }

                operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                message = "Could not load next scene.";
                Debug.LogException(exception, this);
                yield break;
            }
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
