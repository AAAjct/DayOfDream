using UnityEngine;
using UnityEngine.AI;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AnonHunter : MonoBehaviour
    {
        [SerializeField] private AnonHouse house;
        [SerializeField] private FirstPersonExplorer player;
        [SerializeField] private Transform[] patrol;
        [SerializeField] private Animation motion;
        [SerializeField] private AudioSource footsteps;
        [SerializeField] private AudioClip step, impact;
        private bool struck;
        [SerializeField] private Bounds bedroom = new Bounds(new Vector3(-4.215f, 1, 3.5f), new Vector3(5.57f, 4, 7));
        private NavMeshPath patrolPath;
        private HingedDoor[] doors;
        private NavMeshAgent agent;
        private int waypoint;
        private float alertUntil, nextThink, nextStep, bedroomAccessUntil;
        private Vector3 lastSeen;
        private Vector3 lastPlayerPosition, searchOrigin, searchHeading;
        private float searchUntil;
        private int searchStep;
        private bool sawPlayer;
        private bool pursuing;
        private float lastVisualContact = float.NegativeInfinity;
        private AnimationState playing, idle, walk, run, attack, bedAttack, wardrobeAttack, execution;
        private float extractionDuration, extractionTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            patrolPath = new NavMeshPath();
            doors = FindObjectsByType<HingedDoor>();
            if (motion != null)
            {
                idle = motion["ClubIdle"] ?? motion["Idle"];
                walk = motion["ClubWalk"] ?? motion["Walk"];
                run = motion["ClubRun"] ?? motion["Run"];
                attack = motion["ClubAttack"] ?? motion["Attack"];
                bedAttack = motion["BedExtraction"];
                wardrobeAttack = motion["WardrobeExtraction"];
            }
            if (idle == null || walk == null || run == null || attack == null)
            {
                Debug.LogError("Anon Hunter requires Idle, Walk, Run and Attack animation clips.", this);
                enabled = false;
                return;
            }
            motion.cullingType = AnimationCullingType.AlwaysAnimate;
            attack.wrapMode = WrapMode.ClampForever;
            if (bedAttack != null) bedAttack.wrapMode = WrapMode.ClampForever;
            if (wardrobeAttack != null) wardrobeAttack.wrapMode = WrapMode.ClampForever;
        }

        internal bool CanSeePlayer
        {
            get
            {
                if (house.Hidden || (bedroom.Contains(player.transform.position) && Time.time >= bedroomAccessUntil)) return false;
                Vector3 eye = transform.position + Vector3.up * 1.55f;
                Vector3 target = player.transform.position + Vector3.up * (player.IsCrouched ? .75f : 1.3f);
                Vector3 delta = target - eye;
                float range = player.IsCrouched ? 7 : 11;
                if (Mathf.Abs(delta.y) > 1.3f || delta.sqrMagnitude > range * range) return false;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (flat.sqrMagnitude > .01f && Vector3.Dot(transform.forward, flat.normalized) < .49f) return false;
                return !Physics.Linecast(eye, target, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    || hit.transform.IsChildOf(player.transform);
            }
        }

        internal void Hear(Vector3 point, float radius)
        {
            if (execution != null || (transform.position - point).sqrMagnitude > radius * radius) return;
            if (bedroom.Contains(point)) bedroomAccessUntil = Time.time + 15;
            if (pursuing) return;
            if (!NavMesh.SamplePosition(point, out var hit, 1.7f, agent.areaMask)) return;
            lastSeen = hit.position;
            alertUntil = Time.time + 7;
            searchOrigin = lastSeen;
            searchHeading = (lastSeen - transform.position).normalized;
            searchStep = 0;
            searchUntil = alertUntil + 5;
            sawPlayer = false;
        }

        internal bool NoticesHiding(Vector3 entrance) => CanSeePlayer ||
            (Time.time - lastVisualContact < 2f && (lastPlayerPosition - entrance).sqrMagnitude < 3f * 3f);

        internal void ResetPatrol()
        {
            if (!isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.Warp(patrol[0].position);
            agent.ResetPath();
            waypoint = 1;
            alertUntil = nextThink = nextStep = bedroomAccessUntil = 0;
            pursuing = sawPlayer = false;
            lastVisualContact = float.NegativeInfinity;
            execution = null;
            searchUntil = 0;
            searchStep = 0;
            Play(idle, 0);
        }

        private void Update()
        {
            if (!enabled || agent == null || !agent.isOnNavMesh) return;
            if (player.IsPaused)
            {
                agent.isStopped = true;
                if (playing != null) playing.speed = 0;
                return;
            }
            if (execution != null)
            {
                execution.speed = 1;
                UpdateAttack();
                return;
            }
            if (!house.Running)
            {
                agent.isStopped = true;
                Play(idle, .15f);
                playing.speed = 1;
                return;
            }
            Vector3 destination = Time.time < alertUntil ? lastSeen : patrol[waypoint].position;
            var exposed = house.ExposedHidingSpot;
            if (alertUntil == 0 && Time.time < searchUntil && (agent.hasPath || agent.pathPending))
                destination = agent.destination;
            if (exposed != null)
            {
                Vector3 entrance = exposed.Outside.position;
                Vector3 approach = entrance + exposed.Outside.forward * .7f;
                destination = NavMesh.SamplePosition(approach, out var navPoint, .9f, agent.areaMask) ? navPoint.position : entrance;
                // Keep bedroom access while pursuing a known hiding place.
                if (bedroom.Contains(entrance)) bedroomAccessUntil = Time.time + 3f;
                Vector3 offset = transform.position - entrance;
                if (house.IsReadyForExtraction && offset.sqrMagnitude < 1.25f * 1.25f && Mathf.Abs(offset.y) < .65f &&
                    Vector3.Dot(offset, exposed.Outside.forward) > -.1f && !agent.Raycast(destination, out _))
                {
                    bool bed = exposed.Type == HouseObject.Kind.HideUnderBed;
                    AnimationState clip = bed ? bedAttack : wardrobeAttack;
                    BeginAttack(entrance - transform.position, clip ?? attack, bed ? 1.38f : 1.48f);
                    return;
                }
            }
            foreach (var door in doors)
            {
                if (door == null || (Time.time >= bedroomAccessUntil && !bedroom.Contains(transform.position) &&
                    door.Connects(bedroom))) continue;
                door.OpenForHunter(transform.position, destination);
            }
            Vector3 contact = player.transform.position - transform.position;
            Vector3 facing = Vector3.ProjectOnPlane(contact, Vector3.up);
            if (house.CanHunt && facing.sqrMagnitude < 1.16f * 1.16f && Mathf.Abs(contact.y) < .65f && CanSeePlayer)
            {
                BeginAttack(facing, attack, 0);
                return;
            }
            if (Time.time >= nextThink)
            {
                nextThink = Time.time + .16f;
                if (exposed == null) Think();
                else
                {
                    agent.isStopped = false;
                    agent.speed = 3.85f;
                    if (!agent.pathPending && (!agent.hasPath || (agent.destination - destination).sqrMagnitude > .08f))
                        agent.SetDestination(destination);
                }
            }
            float speed = agent.velocity.magnitude;
            bool running = speed > 2.4f;
            Play(speed < .12f ? idle : running ? run : walk, .16f);
            playing.speed = speed < .12f ? 1 : Mathf.Clamp(speed / (running ? 3.85f : 1.55f), .65f, 1.3f);
            if (speed > .25f && Time.time >= nextStep)
            {
                nextStep = Time.time + (running ? .32f : .53f);
                footsteps.pitch = Random.Range(.86f, 1.06f);
                footsteps.PlayOneShot(step);
            }
        }

        private void Play(AnimationState state, float fade)
        {
            if (playing == state) return;
            state.time = 0;
            state.speed = 1;
            motion.CrossFade(state.name, fade);
            playing = state;
        }

        private void BeginAttack(Vector3 facing, AnimationState clip, float extraction)
        {
            agent.isStopped = true;
            facing.y = 0;
            if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
            if (extraction > 0) house.StartExecution(true);
            else house.BeginExecution();
            execution = clip;
            extractionDuration = extraction;
            extractionTime = 0;
            struck = false;
            Play(clip, .07f);
            if (extraction > 0 && clip == attack) clip.speed = 0;
        }

        private void UpdateAttack()
        {
            if (extractionTime < extractionDuration)
            {
                bool fallback = execution == attack;
                if (fallback)
                {
                    execution.speed = 0;
                    execution.time = 0;
                }
                extractionTime = Mathf.Min(extractionDuration, fallback ? extractionTime + Time.deltaTime : execution.time);
                house.FrameExtraction(transform.position, extractionTime, extractionDuration);
                if (extractionTime >= extractionDuration)
                {
                    player.PlayExecutionAudio();
                    if (!fallback)
                    {
                        execution = attack;
                        Play(attack, .06f);
                    }
                }
                return;
            }
            float time = execution.time;
            player.FrameExecution(transform.position, time);
            if (!struck && time >= 1.95f)
            {
                struck = true;
                if (impact != null) footsteps.PlayOneShot(impact, .9f);
            }
            if (execution.normalizedTime < .999f) return;
            house.FinishExecution();
            execution = null;
            Play(idle, .16f);
        }

        private void OnDisable()
        {
            if (execution != null && player != null) player.StopExecutionAudio();
        }

        private void Think()
        {
            bool sees = house.CanHunt && CanSeePlayer;
            pursuing = sees;
            if (sees)
            {
                Vector3 position = player.transform.position;
                lastVisualContact = Time.time;
                Vector3 movement = Vector3.ProjectOnPlane(position - lastPlayerPosition, Vector3.up);
                if (!sawPlayer || movement.sqrMagnitude > .004f)
                    searchHeading = sawPlayer ? movement.normalized : (position - transform.position).normalized;
                lastPlayerPosition = position;
                sawPlayer = true;
                if (NavMesh.SamplePosition(position, out var hit, 1.4f, agent.areaMask))
                    lastSeen = searchOrigin = hit.position;
                alertUntil = Time.time + 3;
                searchUntil = alertUntil + 6;
                searchStep = 0;
            }
            agent.isStopped = false;
            bool reached = !agent.pathPending && (!agent.hasPath || agent.remainingDistance < .55f);
            if (sees || (Time.time < alertUntil && !reached))
            {
                agent.speed = sees ? 3.85f : 2.55f;
                if (!agent.pathPending && (!agent.hasPath || (agent.destination - lastSeen).sqrMagnitude > .08f))
                    agent.SetDestination(lastSeen);
                return;
            }
            if (!sees && Time.time < alertUntil && reached &&
                (transform.position - lastSeen).sqrMagnitude > .75f &&
                agent.CalculatePath(lastSeen, patrolPath) && patrolPath.status == NavMeshPathStatus.PathComplete)
            {
                agent.speed = 2.55f;
                agent.SetPath(patrolPath);
                return;
            }
            if (Time.time < searchUntil)
            {
                alertUntil = 0;
                agent.speed = 2.05f;
                if (reached)
                {
                    while (searchStep < 4)
                    {
                        int stepIndex = searchStep++;
                        Vector3 heading = Quaternion.Euler(0, stepIndex == 0 ? 0 : stepIndex == 1 ? 70 :
                            stepIndex == 2 ? -70 : 165, 0) * searchHeading;
                        Vector3 probe = searchOrigin + heading * (stepIndex == 0 ? 2.8f : 2f);
                        if (!NavMesh.SamplePosition(probe, out var candidate, .8f, agent.areaMask) ||
                            (bedroom.Contains(candidate.position) && Time.time >= bedroomAccessUntil) ||
                            !agent.CalculatePath(candidate.position, patrolPath) ||
                            patrolPath.status != NavMeshPathStatus.PathComplete) continue;
                        agent.SetPath(patrolPath);
                        return;
                    }
                    searchUntil = 0;
                }
                else return;
            }
            agent.speed = 1.55f;
            if (bedroom.Contains(transform.position))
            {
                agent.SetDestination(new Vector3(-.5f, 0, 3.425f));
                return;
            }
            if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < .55f))
            {
                // Only pursuit and noise investigation may enter the starting bedroom.
                for (int attempt = 0; attempt < patrol.Length; attempt++)
                {
                    waypoint = (waypoint + 1) % patrol.Length;
                    Vector3 destination = patrol[waypoint].position;
                    if (bedroom.Contains(destination) || !agent.CalculatePath(destination, patrolPath) ||
                        patrolPath.status == NavMeshPathStatus.PathInvalid) continue;
                    bool entersBedroom = false;
                    Vector3 previous = transform.position;
                    foreach (Vector3 corner in patrolPath.corners)
                    {
                        Vector3 segment = corner - previous;
                        if (bedroom.Contains(corner) || (segment.sqrMagnitude > .001f &&
                            bedroom.IntersectRay(new Ray(previous, segment.normalized), out float distance) &&
                            distance <= segment.magnitude))
                        { entersBedroom = true; break; }
                        previous = corner;
                    }
                    if (entersBedroom) continue;
                    agent.SetDestination(destination);
                    break;
                }
            }
        }
    }
}
