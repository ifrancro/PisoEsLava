using System;
using UnityEngine;

namespace HeatRise.Characters
{
    /// <summary>Visual animation API. Movement, collision and gameplay size stay with the player.</summary>
    public sealed class MaleCharacterView : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] GameObject hat = null;

        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Grounded = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        MaterialPropertyBlock colors;
        Renderer[] renderers;
        bool hasLocomotionSample;
        bool wasGrounded;
        OwnerNetworkAnimator networkAnimator;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
            renderers = GetComponentsInChildren<Renderer>(true);
            networkAnimator = GetComponentInParent<OwnerNetworkAnimator>();
        }

        void OnEnable()
        {
            hasLocomotionSample = false;
        }

        /// <summary>Call each frame with speed in 0..1 and vertical velocity in metres/second.</summary>
        public void SetLocomotion(float normalizedSpeed, bool isGrounded, float verticalSpeed)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.SetFloat(Speed, Mathf.Clamp01(normalizedSpeed));
            animator.SetBool("Moving", normalizedSpeed > 0.1f);
            animator.SetBool(Grounded, isGrounded);
            animator.SetFloat(VerticalSpeed, verticalSpeed);
            if (hasLocomotionSample)
            {
                if (wasGrounded && !isGrounded && verticalSpeed > 0f) PlayAction("Jump");
                else if (!wasGrounded && isGrounded) PlayAction("Land");
            }
            wasGrounded = isGrounded;
            hasLocomotionSample = true;
        }

        public void PlayGrow() => PlayAction("Grow");
        public void PlayShrink() => PlayAction("Shrink");
        public void PlayReturnNormal() => PlayAction("ReturnNormal");
        public void PlayHit() => PlayAction("Hit");
        public void PlayVictory()
        {
            // Solo freezes the course after winning; the celebration can keep playing.
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            PlayAction("Victory");
        }

        void PlayAction(string action)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (networkAnimator != null && networkAnimator.IsSpawned)
            {
                if (networkAnimator.IsOwner) networkAnimator.SetTrigger(action);
            }
            else animator.SetTrigger(action);
        }

        /// <summary>Reset after teleport/respawn, or leave the looping victory animation.</summary>
        public void ResetPose()
        {
            hasLocomotionSample = false;
            if (animator == null) return;
            animator.Rebind();
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.SetFloat(Speed, 0f);
            animator.SetBool("Moving", false);
            animator.SetBool(Grounded, true);
            animator.SetFloat(VerticalSpeed, 0f);
            animator.Play("Idle", 0, 0f);
        }

        public void SetHatVisible(bool visible)
        {
            if (hat != null) hat.SetActive(visible);
        }

        /// <summary>Tints only named clothing slots; skin, eyes and hair retain their colors.</summary>
        public void SetClothingColors(Color shirt, Color pants)
        {
            if (colors == null) colors = new MaterialPropertyBlock();
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    bool isShirt = material.name.IndexOf("Clothing_Shirt", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isPants = material.name.IndexOf("Clothing_Pants", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isShirt && !isPants) continue;
                    renderer.GetPropertyBlock(colors, i);
                    Color color = isShirt ? shirt : pants;
                    if (material.HasProperty("_Color")) colors.SetColor("_Color", color);
                    if (material.HasProperty("_BaseColor")) colors.SetColor("_BaseColor", color);
                    if (material.IsKeywordEnabled("_EMISSION")) colors.SetColor("_EmissionColor", color * .12f);
                    renderer.SetPropertyBlock(colors, i);
                    colors.Clear();
                }
            }
        }
    }
}
