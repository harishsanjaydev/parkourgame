/*
 * PlatformLeap.cs  — Obstacle Clear Leap
 *
 * Trigger: running + jump pressed + obstacle wall detected ahead
 * Behaviour: player launches a high arc over the obstacle, lands on far side
 *
 * SETUP:
 *  1. VaultActions enum:  Platform_Leap = 1 << 8
 *  2. VaultingController.Start():
 *       if (vaultActions.HasFlag(VaultActions.Platform_Leap))
 *           Add(new PlatformLeap(controller));
 *  3. Tick Platform_Leap in Inspector on VaultingController
 *  4. Optionally tag obstacle objects with "Obstacle" and set obstacleTag below
 *
 * ANIMATOR STATES NEEDED:
 *  "Leap Start"   – takeoff
 *  "Leap Airtime" – looping hang in air
 *  "Leap Land"    – landing recovery
 */

using UnityEngine;

namespace Climbing
{
    public class PlatformLeap : VaultAction
    {
        // ── Detection ────────────────────────────────────────────────────────
        private float  obstacleCheckDist = 3.0f;  // how far ahead to detect a wall
        private float  obstacleMinHeight = 0.5f;  // ignore bumps shorter than this
        private float  obstacleMaxHeight = 4.0f;  // won't leap over walls taller than this
        private string obstacleTag       = "";    // optional: only leap over tagged objects

        // ── Arc ──────────────────────────────────────────────────────────────
        private float arcHeight           = 3.5f;  // peak height of jump arc
        private float leapDuration        = 0.9f;  // total air time in seconds
        private float landMomentumKeep    = 0.85f; // speed kept on landing (0-1)
        private float leapForwardDistance = 8f;    // extra forward distance BEYOND the obstacle

        // ── Layers ───────────────────────────────────────────────────────────
        private LayerMask geometryLayers;

        // ── Runtime state ─────────────────────────────────────────────────────
        private float      travelT = 0f;
        private Vector3    leapOrigin;
        private Vector3    leapTarget;
        private Quaternion leapStartRot;
        private Quaternion leapTargetRot;
        private bool       landing  = false;
        private float      landTimer = 0f;
        private const float LAND_RECOVER = 0.22f;

        public PlatformLeap(ThirdPersonController _controller) : base(_controller)
        {
            geometryLayers = ~0; // all layers — narrow to your environment layer if needed
        }

        // ─────────────────────────────────────────────────────────────────────
        public override bool CheckAction()
        {
            if (controller.isVaulting)                                           return false;
            if (!controller.isGrounded)                                          return false;
            if (controller.dummy)                                                return false;
            if (!controller.characterInput.jump)                                 return false;
            if (controller.characterMovement.GetState() != MovementState.Running) return false;
            if (controller.characterInput.movement == Vector2.zero)              return false;

            Vector3 landingSpot;
            if (!FindObstacleAndLanding(out landingSpot)) return false;

            // Setup
            leapOrigin    = controller.transform.position;
            leapTarget    = landingSpot;
            leapStartRot  = controller.transform.rotation;
            leapTargetRot = Quaternion.LookRotation(
                new Vector3(leapTarget.x - leapOrigin.x, 0f, leapTarget.z - leapOrigin.z).normalized);

            travelT   = 0f;
            landing   = false;
            landTimer = 0f;

            controller.DisableController();
            animator.animator.SetBool("Leap Airtime", true);
            animator.animator.CrossFade("Leap Start", 0.15f);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        public override bool Update()
        {
            if (!controller.isVaulting) return false;

            // Landing recovery phase
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

            // Arc travel
            travelT += Time.deltaTime / leapDuration;
            travelT  = Mathf.Clamp01(travelT);

            controller.transform.position = SampleParabola(leapOrigin, leapTarget, arcHeight, travelT);
            controller.transform.rotation = Quaternion.Slerp(leapStartRot, leapTargetRot, travelT * 3f);

            // Swap to airtime loop mid-arc
            if (travelT > 0.18f && travelT < 0.82f)
                animator.animator.CrossFade("Leap Airtime", 0.2f);

            // Begin landing
            if (travelT >= 1f)
            {
                landing   = true;
                landTimer = 0f;

                controller.transform.position = leapTarget;
                controller.characterMovement.SetKinematic(false);

                animator.animator.SetBool("Leap Airtime", false);
                animator.animator.CrossFade("Leap Land", 0.15f);

                // Carry run momentum through landing
                Vector3 fwd = controller.transform.forward
                            * controller.characterMovement.RunSpeed
                            * landMomentumKeep;
                controller.characterMovement.rb.linearVelocity = new Vector3(fwd.x, -1f, fwd.z);
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        public override bool FixedUpdate()
        {
            if (!controller.isVaulting) return false;
            if (landing) return true;
            controller.characterMovement.rb.position = controller.transform.position;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        bool FindObstacleAndLanding(out Vector3 landingSpot)
        {
            landingSpot = Vector3.zero;

            Vector3 pos     = controller.transform.position;
            Vector3 forward = controller.transform.forward;

            // 1. Detect wall at knee height
            RaycastHit wallHit;
            Vector3 kneeOrigin = pos + Vector3.up * 0.6f;
            if (!Physics.Raycast(kneeOrigin, forward, out wallHit, obstacleCheckDist, geometryLayers))
                return false;

            if (obstacleTag != "" && wallHit.transform.tag != obstacleTag)
                return false;

            // 2. Measure obstacle height by probing upward until clear
            float obstacleHeight = 0f;
            for (float h = 0.3f; h <= obstacleMaxHeight + 0.5f; h += 0.15f)
            {
                if (!Physics.Raycast(pos + Vector3.up * h, forward, obstacleCheckDist * 1.5f, geometryLayers))
                {
                    obstacleHeight = h;
                    break;
                }
            }

            if (obstacleHeight < obstacleMinHeight) return false;
            if (obstacleHeight > obstacleMaxHeight) return false;

            // 3. Estimate obstacle depth (how wide it is)
            float obstacleDepth = 3.0f;
            RaycastHit depthHit;
            Vector3 clearOrigin = pos + Vector3.up * (obstacleHeight + 0.3f);
            if (Physics.Raycast(clearOrigin, forward, out depthHit, 8f, geometryLayers))
                obstacleDepth = depthHit.distance;

            // 4. Cast down to find landing surface on far side
            // landDist = obstacle front + obstacle width + big forward leap distance
            float   landDist  = wallHit.distance + obstacleDepth + leapForwardDistance;
            Vector3 landProbe = pos + forward * landDist + Vector3.up * 5f;

            RaycastHit groundHit;
            if (Physics.Raycast(landProbe, Vector3.down, out groundHit, 10f, geometryLayers))
            {
                landingSpot = groundHit.point;
                return true;
            }

            // Fallback landing at same height
            landingSpot   = pos + forward * landDist;
            landingSpot.y = pos.y;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        void FinishLeap()
        {
            controller.characterMovement.EnableFeetIK();
            controller.characterMovement.stopMotion = false;
            controller.characterMovement.SetKinematic(false);
            controller.dummy         = false;
            controller.allowMovement = true;
            controller.isVaulting    = false;
            controller.isJumping     = false;
            controller.ToggleRun();
        }

        Vector3 SampleParabola(Vector3 start, Vector3 end, float height, float t)
        {
            float   pt     = t * 2f - 1f;
            Vector3 result = start + t * (end - start);
            result.y      += (-pt * pt + 1f) * height;
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
}