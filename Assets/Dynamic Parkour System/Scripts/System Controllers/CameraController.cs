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
        private Vector3 _start;              // FIX 1: was never declared

        public float maxTime = 0.3f;
        private float curTime = 0.0f;
        private bool anim = false;
        private float _tiltTarget = 0f;

        [Header("Speed-Driven Camera")]
        public CinemachineFreeLook freeLookCamera;
        public float minSpeed = 0f;
        public float maxSpeed = 10f;

        [Header("FOV")]
        public float baseFOV = 35f;
        public float maxFOV = 80f;

        [Header("Z Offset (pull-back)")]
        public float baseOffsetZ = 0f;
        public float maxOffsetZ = -6f;
        public float baseOffsetY = 0f;
        public float maxOffsetY = 1.5f;
        public float moveSpeed = 3f;         // FIX 6: renamed from 'speed' to avoid clash

        [Header("Smoothing")]
        public float speedLerpSpeed = 12f;
        private float _smoothedSpeed = 0f;

        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();
        }

        void Update()
        {
            // FIX 3+4: removed the rogue Lerp outside the block,
            // removed MoveTowards fighting Lerp — pick one, Lerp is correct here
            if (anim)
            {
                curTime += Time.deltaTime / maxTime;
                cameraOffset.m_Offset = Vector3.Lerp(_start, _target, curTime);

                if (curTime >= 1.0f)
                {
                    anim = false;
                    cameraOffset.m_Offset = _target; // snap cleanly to target
                }
            }
        }

        public void UpdateSpeedEffects(float speed)
        {
            if (freeLookCamera == null) return;

            bool accelerating = speed > _smoothedSpeed;
            float lerpRate = accelerating ? speedLerpSpeed : speedLerpSpeed * 0.4f;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, Time.deltaTime * lerpRate);

            float t = Mathf.InverseLerp(minSpeed, maxSpeed, _smoothedSpeed);
            t = t * t;

            // FOV
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, t);
            freeLookCamera.m_Lens.FieldOfView = Mathf.Lerp(
                freeLookCamera.m_Lens.FieldOfView, targetFOV, Time.deltaTime * speedLerpSpeed);

            // FIX 5: target.y was calculated but never applied — now both z and y apply
            if (!anim)
            {
                Vector3 target = cameraOffset.m_Offset;
                target.z = Mathf.Lerp(baseOffsetZ, maxOffsetZ, t);
                target.y = Mathf.Lerp(baseOffsetY, maxOffsetY, t);
                cameraOffset.m_Offset = Vector3.Lerp(
                    cameraOffset.m_Offset, target, Time.deltaTime * speedLerpSpeed);
            }

            // Tilt
            float tiltAmount = Mathf.Lerp(0f, 3f, t);
            freeLookCamera.m_Lens.Dutch = Mathf.Lerp(
                freeLookCamera.m_Lens.Dutch, tiltAmount, Time.deltaTime * speedLerpSpeed);
        }

        public void newOffset(bool offset)
        {
            _target = offset ? _offset : _default;
            _start = cameraOffset.m_Offset;  // FIX 2: was cameraOffset.m.Offset (typo)
            anim = true;
            curTime = 0;
        }

        public void SetTilt(float tilt)
        {
            _tiltTarget = tilt;
        }
    }
}