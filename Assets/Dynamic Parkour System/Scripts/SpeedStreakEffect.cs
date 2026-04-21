/*
 * SpeedStreakEffect.cs
 * Attach to your Player GameObject.
 * Drag the SpeedStreaks particle system into the slot in Inspector.
 *
 * Emits wind streaks during PlatformLeap airtime, fades out on land.
 * Also kicks in at high run speed so fast ground movement feels good too.
 */

using UnityEngine;

namespace Climbing
{
    public class SpeedStreakEffect : MonoBehaviour
    {
        [Header("References")]
        public ParticleSystem speedStreaks;

        [Header("Tuning")]
        public float groundSpeedThreshold = 12f; // run speed at which streaks start on ground
        public float airEmissionRate      = 120f; // particles per second during leap
        public float groundEmissionRate   = 60f;  // particles per second when sprinting on ground
        public float fadeOutSpeed         = 8f;   // how fast emission drops to 0 on land

        private ThirdPersonController controller;
        private ParticleSystem.EmissionModule emission;
        private float currentRate = 0f;

        void Start()
        {
            controller = GetComponent<ThirdPersonController>();
            emission   = speedStreaks.emission;
            emission.rateOverTime = 0f;
            speedStreaks.Play();
        }

        void Update()
        {
            float targetRate = 0f;

            if (controller.isVaulting)
            {
                // Full streaks during leap airtime
                targetRate = airEmissionRate;
            }
            else if (controller.isGrounded)
            {
                // Streaks when sprinting fast on ground
                float speed = controller.characterMovement.GetVelocity().magnitude;
                if (speed >= groundSpeedThreshold)
                {
                    float t = Mathf.InverseLerp(groundSpeedThreshold,
                                                controller.characterMovement.RunSpeed,
                                                speed);
                    targetRate = Mathf.Lerp(0f, groundEmissionRate, t);
                }
            }

            // Smooth transition in/out
            float lerpSpeed = (targetRate > currentRate) ? 30f : fadeOutSpeed;
            currentRate = Mathf.Lerp(currentRate, targetRate, Time.deltaTime * lerpSpeed);

            emission.rateOverTime = currentRate;
        }
    }
}