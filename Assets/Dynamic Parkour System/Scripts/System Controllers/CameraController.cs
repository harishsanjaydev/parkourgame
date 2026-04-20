using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace Climbing
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineCameraOffset cameraOffset;  // back to original
        public Vector3 _offset;
        public Vector3 _default;
        private Vector3 _target;
        public float maxTime = 2.0f;
        private float curTime = 0.0f;
        private bool anim = false;
        private float _tiltTarget = 0f;

        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();  // back to original
        }

        void Update()
        {
            if (anim)
            {
                curTime += Time.deltaTime / maxTime;
                cameraOffset.m_Offset = Vector3.Lerp(cameraOffset.m_Offset, _target, curTime);  // back to original
            }

            if (curTime >= 1.0f)
                anim = false;
        }

        public void newOffset(bool offset)
        {
            if (offset)
                _target = _offset;
            else
                _target = _default;
            anim = true;
            curTime = 0;
        }
        public void SetTilt(float tilt)
        {
            _tiltTarget = tilt;
        }
    }
}