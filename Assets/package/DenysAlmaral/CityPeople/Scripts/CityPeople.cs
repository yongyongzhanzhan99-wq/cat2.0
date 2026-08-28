using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CityPeople
{
    [RequireComponent(typeof(Animator))]
    public class CityPeople : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Autoplay random animation clips")]
        private bool autoPlayAnimations = true;

        [SerializeField]
        [Tooltip("Overrides palette materials, skips other objects")]
        private Material paletteOverride;

        public string CurrentPaletteName { get; private set; }

        public const string PeoplePalettePrefix = "people_pal";

        private AnimationClip[] myClips;
        private Animator animator;
        private CityPeopleMovement movement;
        private List<Renderer> paletteMeshes;

        private void Awake()
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
            paletteMeshes = new List<Renderer>();

            foreach (Renderer renderer in allRenderers)
            {
                if (renderer.sharedMaterial == null)
                    continue;

                string materialName = renderer.sharedMaterial.name;

                if (materialName.StartsWith(PeoplePalettePrefix, StringComparison.Ordinal))
                {
                    paletteMeshes.Add(renderer);
                }
            }

            if (paletteMeshes.Count > 0)
            {
                CurrentPaletteName = paletteMeshes[0].sharedMaterial.name;
            }

            if (paletteOverride != null)
            {
                SetPalette(paletteOverride);
            }
        }

        private void Start()
        {
            animator = GetComponent<Animator>();
            movement = GetComponent<CityPeopleMovement>();

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                myClips = animator.runtimeAnimatorController.animationClips;

                if (autoPlayAnimations)
                {
                    PlayAnyClip();
                    StartCoroutine(ShuffleClips());
                }
            }
            else
            {
                Debug.LogWarning("CityPeople requires an Animator with a Runtime Animator Controller.");
            }

            if (autoPlayAnimations && GetComponent<CapsuleCollider>() == null)
            {
                // 用于点击、射线检测或简单碰撞。
                CapsuleCollider characterCollider = gameObject.AddComponent<CapsuleCollider>();
                characterCollider.center = new Vector3(0f, 0.8f, 0f);
                characterCollider.radius = 0.3f;
                characterCollider.height = 1.77f;
                characterCollider.direction = 1;
            }
        }

        public void SetPalette(Material material)
        {
            if (material == null)
                return;

            if (!material.name.StartsWith(PeoplePalettePrefix, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    "Material name should start with '" + PeoplePalettePrefix + "'."
                );
                return;
            }

            CurrentPaletteName = material.name;

            foreach (Renderer renderer in paletteMeshes)
            {
                renderer.material = material;
            }
        }

        public void PlayAnyClip()
        {
            if (myClips == null || myClips.Length == 0)
            {
                Debug.LogWarning("Missing animation clips.");
                return;
            }

            AnimationClip clip = myClips[Random.Range(0, myClips.Length)];

            animator.CrossFadeInFixedTime(
                clip.name,
                1.0f,
                -1,
                Random.value * clip.length
            );

            // 将当前选中的动画交给移动脚本判断。
            if (movement != null)
            {
                movement.SetMovementForClip(clip);
            }
        }

        private IEnumerator ShuffleClips()
        {
            while (true)
            {
                yield return new WaitForSeconds(15.0f + Random.value * 5.0f);
                PlayAnyClip();
            }
        }
    }
}