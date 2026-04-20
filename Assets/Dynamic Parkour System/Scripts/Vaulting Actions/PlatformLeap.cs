/*
 * PlatformLeap.cs
 * Drop into your Climbing namespace alongside your other VaultAction scripts.
 *
 * SETUP:
 *  1. Add  Platform_Leap = 1 << 8  to the VaultActions enum in VaultingController.cs
 *  2. In VaultingController.Start() add:
 *       if (vaultActions.HasFlag(VaultActions.Platform_Leap))
 *           Add(new PlatformLeap(controller));
 *  3. In the Inspector, tick Platform_Leap on your VaultingController.
 *  4. Make sure your platforms/ground have a layer assigned and set leapLayers below.
 *  5. Add animator states:
 *       "Leap Start"   – run-to-jump takeoff  (can reuse PredictedJump)
 *       "Leap Airtime" – long hang / tuck     (looping)
 *       "Leap Land"    – smooth landing        (can reuse Land)
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Climbing
{
    public class Platformleap : VaultAction
    {
        // ── Tuning ──────────────────────────────────────────────────────────
        [Header("Detection")]
        private float detectionRange    = 50f;   // max horizontal distance to scan
        private float minHeightGain     = 0.5f;  // platform must be at least this much higher
        private float maxHeightGain     = 6f;    // won't try to leap to something too tall
        private float arcHeight         = 4.5f;  // parabola peak above the midpoint
        private float leapDuration      = 0.85f; // seconds to travel the arc (tune with anim)
        private float landMomentumKeep  = 0.75f; // fraction of run speed kept on landing (0-1)

        // Layer that counts as "platform surface" — set to match your ground/environment layer
        private LayerMask leapLayers;

        // ── State ────────────────────────────────────────────────────────────
        private float   travelT     = 0f;   // 0→1 progress along arc
        private Vector3 leapOrigin;
        private Vector3 leapTarget;
        private Quaternion leapStartRot;
        private Quaternion leapTargetRot;
        private bool    landing     = false;
        private float   landTimer   = 0f;
        private const float LAND_RECOVER = 0.25f; // seconds of landing recovery

        // ── Constructor ──────────────────────────────────────────────────────
        public Platformleap(ThirdPersonController _controller) : base(_controller)
        {
            // Grab every non-ignore layer as the leap surface by default.
            // Prefer to set this explicitly to your ground/environment layer.
            leapLayers = ~0;
        }

        // ── CheckAction — called every frame by VaultingController ───────────
        public override bool CheckAction()
        {
            // Gate: grounded, running, jump pressed, not already doing something
            if (controller.isVaulting)         return false;
            if (!controller.isGrounded)        return false;
            if (controller.dummy)              return false;
            if (!controller.characterInput.jump) return false;
            if (controller.characterInput.movement == Vector2.zero) return false;
            if (controller.characterMovement.GetState() != MovementState.Running) return false;

            // Find a valid platform ahead
            Vector3 landingSpot = Vector3.zero;
            if (!FindPlatform(out landingSpot))
                return false;

            // Set up the leap
            leapOrigin    = controller.transform.position;
            leapTarget    = landingSpot;
            leapStartRot  = controller.transform.rotation;
            leapTargetRot = Quaternion.LookRotation((leapTarget - leapOrigin).WithY(0).normalized);
            travelT       = 0f;
            landing       = false;
            landTimer     = 0f;

            // Hand off control
            controller.DisableController();

            // Animations — replace state names with whatever you have in your Animator
            animator.animator.CrossFade("Leap Start",   0.15f);

            return true;
        }

        // ── Update — runs while isVaulting == true ────────────────────────────
        public override bool Update()
        {
            if (!controller.isVaulting) return false;

            // ── Landing recovery phase ───────────────────────────────────────
            if (landing)
            {
                landTimer += Time.deltaTime;
                if (landTimer >= LAND_RECOVER)
                {
                    FinishLeap();
                    return false;
                }
                return true;
            }

            // ── Arc travel ──────────────────────────────────────────────────
            travelT += Time.deltaTime / leapDuration;
            travelT  = Mathf.Clamp01(travelT);

            // Move along parabola
            controller.transform.position = SampleParabola(leapOrigin, leapTarget, arcHeight, travelT);

            // Rotate toward target
            controller.transform.rotation = Quaternion.Slerp(leapStartRot, leapTargetRot, travelT * 3f);

            // Swap to airtime anim once we're past takeoff
            if (travelT > 0.15f && travelT < 0.85f)
                animator.animator.SetBool("Leap Airtime", true);

            // Trigger landing phase
            if (travelT >= 1f)
            {
                landing = true;
                landTimer = 0f;
                animator.animator.SetBool("Leap Airtime", false);

                // Snap to exact landing spot
                controller.transform.position = leapTarget;

                // Re-enable physics so momentum carries through
                controller.characterMovement.SetKinematic(false);

                // Keep horizontal momentum — feels fast, not dead-stop
                Vector3 forwardMomentum = controller.transform.forward
                                        * controller.characterMovement.RunSpeed
                                        * landMomentumKeep;
                controller.characterMovement.rb.linearVelocity =
                    new Vector3(forwardMomentum.x, -1f, forwardMomentum.z);
            }

            return true;
        }

        // ── FixedUpdate — keep rigidbody in sync during arc ──────────────────
        public override bool FixedUpdate()
        {
            if (!controller.isVaulting) return false;
            if (landing)                return true;

            // Mirror transform position into rigidbody (kinematic during arc)
            controller.characterMovement.rb.position = controller.transform.position;

            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Casts forward to find a higher platform within range.
        /// Returns true and sets landingSpot if valid target found.
        /// </summary>
        bool FindPlatform(out Vector3 landingSpot)
        {
            landingSpot = Vector3.zero;

            Vector3 playerPos  = controller.transform.position;
            Vector3 forward    = controller.transform.forward;

            // Sphere-scan forward along the ground plane at several distances
            int   steps     = 12;
            float stepSize  = detectionRange / steps;

            for (int i = 2; i <= steps; i++)   // start at step 2 to skip feet
            {
                float   dist   = stepSize * i;
                Vector3 probe  = playerPos + forward * dist + Vector3.up * maxHeightGain;

                RaycastHit hit;
                // Cast downward from above to find a surface
                if (Physics.Raycast(probe, Vector3.down, out hit, maxHeightGain * 2f, leapLayers))
                {
                    float heightDiff = hit.point.y - playerPos.y;

                    if (heightDiff < minHeightGain) continue;  // not high enough
                    if (heightDiff > maxHeightGain) continue;  // too high

                    // Make sure there's nothing blocking the path at chest height
                    Vector3 chestOrigin = playerPos + Vector3.up * 1.2f;
                    if (Physics.Raycast(chestOrigin, forward, dist * 0.5f, leapLayers))
                        continue; // wall in the way

                    landingSpot = hit.point;
                    return true;
                }
            }

            return false;
        }

        void FinishLeap()
        {
            controller.characterMovement.EnableFeetIK();
            controller.characterMovement.stopMotion = false;
            controller.dummy        = false;
            controller.allowMovement = true;
            // Don't call EnableController() — we already re-enabled physics above
            // just need to restore the state flags
            controller.isVaulting = false;
            controller.isJumping  = false;
            controller.characterMovement.SetKinematic(false);

            // Resume running immediately
            controller.ToggleRun();
        }

        /// <summary>Parabola sample — same formula as JumpPredictionController.</summary>
        Vector3 SampleParabola(Vector3 start, Vector3 end, float height, float t)
        {
            float   parabolicT  = t * 2f - 1f;
            Vector3 travel      = end - start;
            Vector3 result      = start + t * travel;
            result.y           += (-parabolicT * parabolicT + 1f) * height;
            return result;
        }

        public override void DrawGizmos()
        {
            if (leapTarget == Vector3.zero) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(leapTarget, 0.12f);

            Vector3 last = leapOrigin;
            for (int i = 1; i <= 20; i++)
            {
                Vector3 p = SampleParabola(leapOrigin, leapTarget, arcHeight, i / 20f);
                Gizmos.DrawLine(last, p);
                last = p;
            }
        }
    }

    // Small extension so .WithY() reads cleanly
    internal static class Vec3Ext
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}