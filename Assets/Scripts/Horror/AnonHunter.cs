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
        private AnimationState playing, idle, walk, run, attack;

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
            }
            if (idle == null || walk == null || run == null || attack == null)
            {
                Debug.LogError("Anon Hunter requires Idle, Walk, Run and Attack animation clips.", this);
                enabled = false;
                return;
            }
            motion.cullingType = AnimationCullingType.AlwaysAnimate;
            attack.wrapMode = WrapMode.ClampForever;
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
            if (playing == attack || (transform.position - point).sqrMagnitude > radius * radius) return;
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

        internal void ResetPatrol()
        {
            if (!isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.Warp(patrol[0].position);
            agent.ResetPath();
            waypoint = 1;
            alertUntil = nextThink = nextStep = bedroomAccessUntil = 0;
            pursuing = sawPlayer = false;
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
            if (attack != null && playing == attack)
            {
                attack.speed = 1;
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
            if (alertUntil == 0 && Time.time < searchUntil && (agent.hasPath || agent.pathPending))
                destination = agent.destination;
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
                agent.isStopped = true;
                if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
                house.BeginExecution();
                struck = false;
                Play(attack, .07f);
                return;
            }
            if (Time.time >= nextThink)
            {
                nextThink = Time.time + .16f;
                Think();
            }
            if (playing == attack) return;
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

        private void UpdateAttack()
        {
            player.FrameExecution(transform.position, attack.time);
            if (!struck && attack.time >= 1.95f)
            {
                struck = true;
                if (impact != null) footsteps.PlayOneShot(impact, .9f);
            }
            if (attack.normalizedTime < .999f) return;
            house.FinishExecution();
            Play(idle, .16f);
        }

        private void OnDisable()
        {
            if (playing != null && playing == attack && player != null) player.StopExecutionAudio();
        }

        private void Think()
        {
            bool sees = house.CanHunt && CanSeePlayer;
            pursuing = sees;
            if (sees)
            {
                Vector3 position = player.transform.position;
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
