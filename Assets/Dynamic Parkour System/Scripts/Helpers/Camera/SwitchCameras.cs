using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace Climbing
{
    public class SwitchCameras : MonoBehaviour
    {
        Animator animator;
        Rigidbody rb;                          // player rigidbody
        CameraController cameraController;     // sibling/child component

        enum CameraType { None, Freelook, Slide }
        CameraType curCam = CameraType.None;

        [SerializeField] private CinemachineFreeLook FreeLook;
        [SerializeField] private CinemachineVirtualCamera Slide;

        void Start()
        {
            animator         = GetComponent<Animator>();
            rb               = GetComponent<Rigidbody>();

            // Find CameraController — attach it to the same GameObject as SwitchCameras,
            // or assign via [SerializeField] if it lives elsewhere
            cameraController = FindObjectOfType<CameraController>();

            FreeLookCam();
        }

        void Update()
        {
            // Only drive speed effects while on the FreeLook cam
            if (curCam == CameraType.Freelook && rb != null && cameraController != null)
            {
                // Use horizontal speed only — ignores jump/fall velocity
                Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                cameraController.UpdateSpeedEffects(horizontal.magnitude);
            }
        }

        public void FreeLookCam()
        {
            if (curCam != CameraType.Freelook)
            {
                Slide.Priority    = 0;
                FreeLook.Priority = 1;
                curCam            = CameraType.Freelook;   // ← was missing in original
            }
        }

        public void SlideCam()
        {
            if (curCam != CameraType.Slide)
            {
                FreeLook.Priority = 0;
                Slide.Priority    = 1;
                curCam            = CameraType.Slide;      // ← was missing in original
            }
        }
    }
}