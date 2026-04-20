using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace Climbing
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineCameraOffset cameraOffset;
        public Vector3 _offset;
        public Vector3 _default;
        private Vector3 _target;
        public float maxTime = 2.0f;
        private float curTime = 0.0f;
        private bool anim = false;
        private float _tiltTarget = 0f;

        [Header("Speed-Driven Camera")]
        public CinemachineFreeLook freeLookCamera;   // drag your FreeLook cam here
        public float minSpeed = 0f;
        public float maxSpeed = 10f;

        [Header("FOV")]
        public float baseFOV = 40f;
        public float maxFOV  = 65f;

        [Header("Z Offset (pull-back)")]
        public float baseOffsetZ = 0f;    // Z when standing still
        public float maxOffsetZ  = -3f;   // Z at full speed (negative = further back)

        [Header("Smoothing")]
        public float speedLerpSpeed = 5f; // how fast camera reacts to speed changes

        private float _smoothedSpeed = 0f;

        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();
        }

        void Update()
        {
            // --- existing manual animation ---
            if (anim)
            {
                curTime += Time.deltaTime / maxTime;
                cameraOffset.m_Offset = Vector3.Lerp(cameraOffset.m_Offset, _target, curTime);
            }
            if (curTime >= 1.0f)
                anim = false;
        }

        // Call this every frame from SwitchCameras (or a PlayerController)
        // passing in the player's current world-space speed
        public void UpdateSpeedEffects(float speed)
        {
            if (freeLookCamera == null) return;

            // Smooth the raw speed value so camera doesn't snap
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, Time.deltaTime * speedLerpSpeed);

            float t = Mathf.InverseLerp(minSpeed, maxSpeed, _smoothedSpeed); // 0..1

            // --- FOV ---
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, t);
            freeLookCamera.m_Lens.FieldOfView = Mathf.Lerp(
                freeLookCamera.m_Lens.FieldOfView, targetFOV, Time.deltaTime * speedLerpSpeed);

            // --- Pull-back offset (Z axis) ---
            // Only drive offset when no manual anim is running
            if (!anim)
            {
                Vector3 target = cameraOffset.m_Offset;
                target.z = Mathf.Lerp(baseOffsetZ, maxOffsetZ, t);
                cameraOffset.m_Offset = Vector3.Lerp(
                    cameraOffset.m_Offset, target, Time.deltaTime * speedLerpSpeed);
            }
        }

        public void newOffset(bool offset)
        {
            _target = offset ? _offset : _default;
            anim = true;
            curTime = 0;
        }

        public void SetTilt(float tilt)
        {
            _tiltTarget = tilt;
        }
    }
}