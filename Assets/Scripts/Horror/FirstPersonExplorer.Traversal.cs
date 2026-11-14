using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed partial class FirstPersonExplorer
    {
        private enum Traversal { None, Vault, Climb, Glide }

        private const int ObstacleMask = ~(1 << 2);
        private const float CrouchHeight = 1.15f;
        private const float StandingHeight = 1.8f;
        private const float GlideHeight = .82f;
        private const float MaxObstacleHeight = 1.22f;
        private const float MaxCrossDepth = 2.8f;
        private const float SurfaceStep = .05f;
        private const float TraversalReach = .90f;
        private const float TraversalLeadTime = .27f;

        private Traversal traversal;
        private Vector3 traversalStart, traversalDirection, traversalLanding;
        private Vector3 traversalContact, traversalViewOffset, traversalCameraVelocity;
        private float traversalTime, traversalDuration, traversalDistance, traversalLift, traversalStartEye, traversalApproach;
        private float traversalRiseEnd, traversalCrossEnd;
        private float traversalLeadDistance, traversalLeadSpeed;
        private bool traversalRunning;

        internal bool IsClimbing => traversal == Traversal.Climb;
        internal bool IsVaulting => traversal == Traversal.Vault;
        internal bool IsGliding => traversal == Traversal.Glide;
        internal float TraversalProgress => traversal == Traversal.None ? 0 : traversalTime / traversalDuration;

        private bool TryStartTraversal(Vector3 direction, bool running, float moveAmount)
        {
            Vector3 start = transform.position;
            float approachSpeed = (running ? runSpeed : walkSpeed) * moveAmount;
            if (!TryGetObstacleFront(start, direction, TraversalReach + approachSpeed * TraversalLeadTime, out var front) ||
                Mathf.Abs(front.normal.y) > .35f ||
                front.collider.GetComponentInParent<HingedDoor>() != null ||
                front.collider.GetComponentInParent<HouseObject>() != null)
                return false;

            float leadDistance = Mathf.Max(0f, front.distance - TraversalReach);
            if (leadDistance > 0f)
            {
                Vector3 takeoff = start + direction * leadDistance;
                if (!TryGetSurface(takeoff, out var support) || support.normal.y < .8f ||
                    Mathf.Abs(support.point.y - start.y) > .06f)
                    return false;
                start = takeoff;
            }
            float frontDistance = front.distance - leadDistance;

            // A platform deep enough to stand on can be climbed without finding its far edge.
            float distance = frontDistance + .005f;
            if (!TryGetSurface(start + direction * distance, out var top)) return false;
            float height = top.point.y - start.y;
            if (height < .22f || height > MaxObstacleHeight || top.normal.y < .8f) return false;

            Traversal action = GetTraversalAction(front.collider.transform);
            bool climb = action == Traversal.Climb;
            float surfaceHeight = height;
            float landingDistance;
            if (climb)
            {
                landingDistance = frontDistance + motor.radius + .06f;
            }
            else
            {
                for (; distance - frontDistance <= MaxCrossDepth; distance += SurfaceStep)
                {
                    if (!TryGetSurface(start + direction * distance, out var surface) || surface.normal.y < .8f)
                        return false;
                    height = surface.point.y - start.y;
                    if (height <= .16f) break;
                    // A seat and a higher backrest can form one obstacle.
                    if (height > MaxObstacleHeight) return false;
                    surfaceHeight = Mathf.Max(surfaceHeight, height);
                }
                if (height > .16f) return false;
                landingDistance = distance + motor.radius + .08f;
            }
            if (!TryGetSurface(start + direction * landingDistance, out var landing) ||
                landing.normal.y < .8f)
                return false;
            float landingHeight = landing.point.y - start.y;
            if (climb ? landingHeight < .22f || landingHeight > MaxObstacleHeight : Mathf.Abs(landingHeight) > .26f)
                return false;

            surfaceHeight = Mathf.Max(surfaceHeight, landingHeight);
            float controllerHeight = action == Traversal.Glide ? GlideHeight : CrouchHeight;
            Vector3 lower = Vector3.up * motor.radius;
            Vector3 upper = Vector3.up * (controllerHeight - motor.radius);
            float radius = motor.radius - motor.skinWidth;
            float lift = surfaceHeight + motor.skinWidth + .05f;
            Vector3 raisedStart = start + Vector3.up * lift;
            // The centerline can miss a low object that still clips the side of the capsule.
            while (Physics.CapsuleCast(raisedStart + lower, raisedStart + upper, radius, direction, out var blocker,
                       landingDistance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                float blockerHeight = blocker.collider.bounds.max.y - start.y;
                if (blockerHeight <= surfaceHeight || blockerHeight > MaxObstacleHeight) return false;
                surfaceHeight = blockerHeight;
                lift = surfaceHeight + motor.skinWidth + .05f;
                raisedStart = start + Vector3.up * lift;
            }
            Vector3 raisedEnd = raisedStart + direction * landingDistance;
            Vector3 landingPosition = landing.point + Vector3.up * motor.skinWidth;
            if (Physics.CapsuleCast(start + lower, start + upper, radius, Vector3.up, lift,
                    ObstacleMask, QueryTriggerInteraction.Ignore) ||
                !HasCapsuleClearance(raisedStart + lower, raisedStart + upper, radius) ||
                Physics.CapsuleCast(raisedEnd + lower, raisedEnd + upper, radius, Vector3.down, raisedEnd.y - landingPosition.y,
                    ObstacleMask, QueryTriggerInteraction.Ignore) ||
                !HasCapsuleClearance(landingPosition + lower, landingPosition + upper, radius))
                return false;

            traversal = action;
            traversalStart = start;
            traversalDirection = direction;
            traversalLanding = landingPosition;
            traversalDistance = landingDistance;
            traversalLift = lift;
            traversalApproach = Mathf.Max(0f, frontDistance - motor.radius - .09f);
            traversalLeadDistance = leadDistance;
            traversalLeadSpeed = approachSpeed;
            traversalRunning = running;
            traversalTime = 0f;
            float riseTime = (climb ? .24f : .10f) + lift * (climb ? .13f : .09f);
            float crossSpeed = climb ? 3.5f : action == Traversal.Glide ? 5.1f : 5.6f;
            float crossTime = Mathf.Max(.12f, (landingDistance - traversalApproach) / crossSpeed);
            float drop = raisedEnd.y - landingPosition.y;
            float settleTime = climb ? .11f : Mathf.Max(.12f, Mathf.Sqrt(2f * drop / Gravity) * .77f);
            float motionDuration = riseTime + crossTime + settleTime;
            traversalDuration = motionDuration * (running ? .78f : 1f);
            traversalRiseEnd = riseTime / motionDuration;
            traversalCrossEnd = (riseTime + crossTime) / motionDuration;
            traversalContact = top.point;
            if (leadDistance == 0f) BeginTraversalMotion();
            return true;
        }

        private void BeginTraversalMotion()
        {
            traversalViewOffset = view.localPosition;
            traversalCameraVelocity = Vector3.zero;
            traversalStartEye = view.localPosition.y;
            crouched = true;
            SetControllerHeight(IsGliding ? GlideHeight : CrouchHeight);
            fallSpeed = 0f;
        }

        private static Traversal GetTraversalAction(Transform obstacle)
        {
            for (var current = obstacle; current != null; current = current.parent)
            {
                if (current.CompareTag("Climb")) return Traversal.Climb;
                if (current.CompareTag("Glide")) return Traversal.Glide;
            }
            return Traversal.Vault;
        }

        private static bool TryGetObstacleFront(Vector3 start, Vector3 direction, float reach, out RaycastHit front)
        {
            front = default;
            float nearest = reach;
            // A rail can have open space under its top bar.
            for (float height = .12f; height <= MaxObstacleHeight; height += .08f)
            {
                if (!Physics.Raycast(start + Vector3.up * height, direction, out var hit,
                        nearest, ObstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
                front = hit;
                nearest = hit.distance;
            }
            return front.collider != null;
        }

        private static bool TryGetSurface(Vector3 position, out RaycastHit hit)
        {
            return Physics.Raycast(position + Vector3.up * (MaxObstacleHeight + .15f), Vector3.down, out hit,
                MaxObstacleHeight + .55f, ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateTraversal(Vector3 moveDirection)
        {
            float deltaTime = Time.deltaTime;
            Vector3 before = transform.position;
            if (traversalLeadDistance > 0f)
            {
                if (Vector3.Dot(moveDirection.normalized, traversalDirection) < .75f)
                {
                    MovementSpeed = 0f;
                    traversal = Traversal.None;
                    return;
                }

                float step = Mathf.Min(traversalLeadDistance, traversalLeadSpeed * deltaTime);
                var flags = motor.Move(traversalDirection * step + Vector3.down * (2f * deltaTime));
                Vector3 movement = transform.position - before;
                grounded = (flags & CollisionFlags.Below) != 0;
                if (!grounded || Vector3.Dot(movement, traversalDirection) < step - .02f)
                {
                    MovementSpeed = new Vector2(movement.x, movement.z).magnitude / Mathf.Max(deltaTime, .001f);
                    traversal = Traversal.None;
                    return;
                }
                traversalLeadDistance -= step;
                deltaTime -= step / traversalLeadSpeed;
                if (traversalLeadDistance == 0f) BeginTraversalMotion();
            }

            // Spend the full frame budget while sweeping each phase separately.
            while (deltaTime > .00001f && traversal != Traversal.None && traversalLeadDistance == 0f)
            {
                float consumed = AdvanceTraversal(deltaTime);
                deltaTime -= consumed;
            }
            Vector3 actual = transform.position - before;
            MovementSpeed = new Vector2(actual.x, actual.z).magnitude / Mathf.Max(Time.deltaTime, .001f);
            if (traversal == Traversal.None) return;
            if (traversalLeadDistance > 0f)
            {
                UpdateView(traversalRunning);
                return;
            }
            ApplyTraversalView();
        }

        private float AdvanceTraversal(float deltaTime)
        {
            float riseEnd = traversalDuration * traversalRiseEnd;
            float crossEnd = traversalDuration * traversalCrossEnd;
            float phaseEnd = traversalTime < riseEnd ? riseEnd : traversalTime < crossEnd ? crossEnd : traversalDuration;
            float previousTime = traversalTime;
            traversalTime = Mathf.Min(phaseEnd, traversalTime + deltaTime);
            float progress = TraversalProgress;
            float crossing = Mathf.InverseLerp(traversalRiseEnd, traversalCrossEnd, progress);
            float cross = Mathf.SmoothStep(0f, 1f, crossing);
            if (IsGliding)
                cross = Mathf.Lerp(cross, crossing, .65f);
            float rise = Smooth(progress, 0f, traversalRiseEnd);
            float descend = Mathf.InverseLerp(traversalCrossEnd, 1f, progress);
            float approach = traversalApproach * Mathf.InverseLerp(0f, traversalRiseEnd, progress);
            float distance = approach + (traversalDistance - traversalApproach) * cross;
            Vector3 desired = traversalStart + traversalDirection * distance;
            desired.y += traversalLift * rise;
            desired.y = Mathf.Lerp(desired.y, traversalLanding.y, IsClimbing ? Mathf.SmoothStep(0f, 1f, descend) : descend * descend);
            CollisionFlags flags = motor.Move(desired - transform.position);
            grounded = (flags & CollisionFlags.Below) != 0;

            if (progress >= 1f || (desired - transform.position).sqrMagnitude > .10f * .10f)
            {
                ApplyTraversalView();
                traversal = Traversal.None;
                SetControllerHeight(CrouchHeight);
                fallSpeed = 0f;
                eyeVelocity = 0f;
            }
            return traversalTime - previousTime;
        }

        private void ApplyTraversalView()
        {
            float progress = TraversalProgress;
            float rise = Smooth(progress, 0f, traversalRiseEnd);
            float descend = Mathf.InverseLerp(traversalCrossEnd, 1f, progress);
            // Change head position with the body pose. Keep the player's aim and the horizon stable.
            float load = Smooth(rise, 0f, .36f) * (1f - Smooth(rise, .42f, .96f));
            float compression = Smooth(progress, 0f, traversalRiseEnd) *
                (1f - Smooth(descend, IsGliding ? .38f : .10f, 1f));
            float eyeHeight = Mathf.Lerp(traversalStartEye, IsGliding ? .72f : IsClimbing ? 1.15f : 1.29f, compression)
                - load * (IsClimbing ? .14f : .08f);
            if (descend > 0f)
            {
                float endingEye = crouchToggled || !CanStand() ? .98f : 1.60f;
                eyeHeight = Mathf.Lerp(eyeHeight, endingEye, Smooth(descend, IsGliding ? .50f : .28f, 1f));
            }

            Quaternion heading = Quaternion.Euler(0f, rearYaw, 0f);
            Vector3 direction = transform.InverseTransformDirection(traversalDirection);
            float lean = IsGliding ? -.035f * compression : .11f * load + .05f * compression;
            Vector3 target = Vector3.up * eyeHeight + heading * (Vector3.forward * .055f) + direction * lean;
            float entry = Smooth(progress, 0f, .12f);
            Vector3 offset = Vector3.Lerp(traversalViewOffset, target, entry);
            view.localPosition = Vector3.SmoothDamp(view.localPosition, offset, ref traversalCameraVelocity, traversalRunning ? .034f : .045f);
            smoothedEyeY = view.position.y;

            Vector3 sightline = Quaternion.Inverse(heading) *
                transform.InverseTransformDirection(traversalContact - view.position);
            float lookAtEdge = Mathf.Atan2(-sightline.y, Mathf.Max(.18f, sightline.z)) * Mathf.Rad2Deg;
            float attention = Mathf.Max(0f, Vector3.Dot(heading * Vector3.forward, direction));
            float pitchOffset = Mathf.Clamp(lookAtEdge - pitch, -7f, 9f) * load * attention * (IsGliding ? .2f : 1f);
            view.localRotation = heading * Quaternion.Euler(pitch + pitchOffset, 0f, 0f);
        }

        private void SetControllerHeight(float height)
        {
            motor.height = height;
            motor.center = Vector3.up * (height * .5f);
        }
    }
}
