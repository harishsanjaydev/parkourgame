using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Climbing
{
    public class WallRun : VaultAction
    {
        // Wall detection
        private RaycastHit wallHit;
        private bool wallOnLeft = false;
        private bool wallOnRight = false;
        private float wallRayLength = 1.1f;

        // Wall run state
        private float wallRunTimer = 0f;
        private float maxWallRunTime = 2f;
        private float wallRunSpeed = 14f;
        private float jumpOffForce = 12f;

        // Camera tilt
        private float currentTilt = 0f;
        private float tiltAmount = 15f;
        private float tiltSpeed = 5f;

        public WallRun(ThirdPersonController _controller) : base(_controller) // FIXED
        {
        }

        public override bool CheckAction()
        {
            // Must be in the air and not already doing something
            if (controller.isGrounded || controller.isVaulting)
                return false;

            Vector3 origin = controller.transform.position + new Vector3(0, 0.8f, 0);

            // Check right wall first
            wallOnRight = controller.characterDetection.ThrowRayOnDirection(
                origin, controller.transform.right, wallRayLength, out wallHit);

            // Then left
            if (!wallOnRight)
            {
                wallOnLeft = controller.characterDetection.ThrowRayOnDirection(
                    origin, -controller.transform.right, wallRayLength, out wallHit);
            }
            else
            {
                wallOnLeft = false;
            }

            if (wallOnLeft || wallOnRight)
            {
                wallRunTimer = 0f;

                // Turn off gravity so player sticks to wall
                controller.characterMovement.rb.useGravity = false;
                controller.characterMovement.rb.linearVelocity = Vector3.zero;

                controller.characterAnimation.animator.CrossFade(
                    wallOnRight ? "WallRunRight" : "WallRunLeft", 0.15f);

                // Disable normal movement but keep controller alive
                controller.allowMovement = false;
                controller.dummy = true;

                return true;
            }

            return false;
        }

        public override bool Update()
        {
            wallRunTimer += Time.deltaTime;

            // Re-detect wall each frame
            if (!DetectWall())          return EndWallRun();
            if (controller.isGrounded)  return EndWallRun();
            if (wallRunTimer > maxWallRunTime) return EndWallRun();

            // Jump off wall
            if (controller.characterInput.jump)
            {
                JumpOffWall();
                return false;
            }

            // Direction along the wall surface
            Vector3 wallForward = Vector3.Cross(wallHit.normal, Vector3.up);

            // Flip if pointing wrong way
            if (Vector3.Dot(wallForward, controller.transform.forward) < 0)
                wallForward = -wallForward;

            // Move along wall, neutralize gravity manually
            controller.characterMovement.rb.linearVelocity =
                wallForward * wallRunSpeed + Vector3.up * 0f;

            // Face along wall
            if (wallForward != Vector3.zero)
                controller.transform.rotation = Quaternion.LookRotation(wallForward);

            // Camera tilt
            float targetTilt = wallOnRight ? -tiltAmount : tiltAmount;
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
            controller.cameraController.SetTilt(currentTilt);

            return true;
        }

        public override bool FixedUpdate()
        {
            return controller.isVaulting;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private bool DetectWall()
        {
            Vector3 origin = controller.transform.position + new Vector3(0, 0.8f, 0);

            if (wallOnRight)
                return controller.characterDetection.ThrowRayOnDirection(
                    origin, controller.transform.right, wallRayLength, out wallHit);
            else
                return controller.characterDetection.ThrowRayOnDirection(
                    origin, -controller.transform.right, wallRayLength, out wallHit);
        }

        private void JumpOffWall()
        {
            // Push away from wall and upward
            Vector3 jumpDir = (wallHit.normal + Vector3.up).normalized;
            controller.characterMovement.rb.useGravity = true;
            controller.characterMovement.rb.linearVelocity = jumpDir * jumpOffForce;
            EndWallRun();
        }

        private bool EndWallRun()
        {
            wallOnLeft = false;
            wallOnRight = false;

            // Restore gravity and control
            controller.characterMovement.rb.useGravity = true;
            controller.allowMovement = true;
            controller.dummy = false;

            // Reset camera tilt
            controller.cameraController.SetTilt(0f);

            controller.characterAnimation.animator.CrossFade("Fall", 0.2f);
            return false;
        }
    }
}