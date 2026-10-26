using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Lite random patrol: while the zombie has no detected survivor it wanders
// between random NavMesh points around its spawn home, pausing a few seconds
// between legs. Points are sampled around HOME (never around the zombie's
// current position, so patrols cannot drift across the map) and a leg only
// starts when a complete NavMesh path to the point exists.
//
// Server-authoritative like the rest of the zombie simulation; in pure local
// play (no NetworkManager) it runs locally. Movement itself reuses the
// existing CommandedMove state through ZombieBehavior.SetMoveDestination, and
// detection interrupts a leg immediately: the moment the FSM acquires a
// target, the patrol destination is cleared and the FSM routes
// Idle -> Chasing as usual. Destinations set by anyone else (e.g.
// click-to-move) are left untouched: the patrol only clears its own legs.
//
// No [RequireComponent(typeof(ZombieBehavior))]: the death teardown
// (ZombieBrain) deliberately Destroys the ZombieBehavior component, and a
// RequireComponent dependency makes Unity refuse that destroy. The behavior
// reference is resolved in Awake and null-guarded everywhere instead.
public class ZombiePatrol : NetworkBehaviour
{
    private const int MaxPickAttempts = 8;
    private const float SampleMaxDistance = 1.0f;
    private const float LegTimeoutSeconds = 25f;
    private const float ArrivalSlack = 0.1f;
    private const float DefaultRadius = 8f;
    private const float DefaultPauseMin = 2f;
    private const float DefaultPauseMax = 5f;

    // Created in Awake: NavMeshPath's constructor touches the NavMesh system
    // and throws from a field initializer (MonoBehaviour construction).
    private NavMeshPath _path;

    private ZombieBehavior _behavior;
    private bool _localMode;
    private bool _homeSet;
    private Vector3 _home;
    private bool _legActive;
    private float _legTimer;
    private float _pauseTimer;

    // Random point on the disc of the given radius around home (XZ plane).
    public static Vector3 CandidatePoint(Vector3 home, float radius, Vector2 unitCirclePoint)
    {
        return home + new Vector3(unitCirclePoint.x * radius, 0f, unitCirclePoint.y * radius);
    }

    // Pause length for a unit-random roll in [0, 1].
    public static float RandomPause(float min, float max, float unitRandom)
    {
        return Mathf.Lerp(min, max, Mathf.Clamp01(unitRandom));
    }

    private bool HasAuthority => _localMode || IsServer;

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _path = new NavMeshPath();
    }

    private void Start()
    {
        // No NetworkManager in the scene = single-player / playground: the
        // NetworkBehaviour never spawns, so run the patrol locally.
        _localMode = NetworkManager.Singleton == null;
        if (_localMode)
        {
            CaptureHome();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CaptureHome();
        }
    }

    private void CaptureHome()
    {
        _home = transform.position;
        _homeSet = true;
        // Each zombie rolls its own first pause, so a freshly spawned horde
        // does not start walking in lockstep.
        _pauseTimer = FreshPause();
    }

    private float FreshPause()
    {
        ZombieData data = _behavior != null ? _behavior.zombieData : null;
        float min = data != null ? data.patrolPauseMin : DefaultPauseMin;
        float max = data != null ? data.patrolPauseMax : DefaultPauseMax;
        return RandomPause(min, max, Random.value);
    }

    private void Update()
    {
        if (!HasAuthority || _behavior == null || !_homeSet) return;

        ZombieContext context = _behavior._context;
        if (context == null) return;

        ZombieData data = _behavior.zombieData;
        if (data != null && !data.patrolEnabled)
        {
            EndLeg();
            return;
        }

        // Detection (or death) interrupts a patrol leg the moment it happens.
        if (!context.isAlive || context.target != null)
        {
            EndLeg();
            _pauseTimer = FreshPause();
            return;
        }

        if (_legActive)
        {
            _legTimer += Time.deltaTime;
            NavMeshAgent agent = context.agent;
            // Arrival is only meaningful once the move state has issued the
            // destination and the agent holds a path: before that,
            // remainingDistance reads 0 and would fake an instant arrival.
            bool arrived = _legTimer > 0.5f
                && agent != null && agent.isOnNavMesh && agent.hasPath && !agent.pathPending
                && agent.remainingDistance <= agent.stoppingDistance + ArrivalSlack;
            if (arrived || _legTimer > LegTimeoutSeconds)
            {
                EndLeg();
                _pauseTimer = FreshPause();
            }
            return;
        }

        // Someone else is commanding this zombie (click-to-move, a director):
        // stay out of the way until their destination clears.
        if (context.moveDestination != null) return;

        _pauseTimer -= Time.deltaTime;
        if (_pauseTimer > 0f) return;

        if (TryPickDestination(data, out Vector3 destination))
        {
            _behavior.SetMoveDestination(destination);
            _legActive = true;
            _legTimer = 0f;
        }
        else
        {
            _pauseTimer = 1f;
        }
    }

    private void EndLeg()
    {
        if (!_legActive) return;
        _legActive = false;
        _behavior.ClearMoveDestination();
    }

    private bool TryPickDestination(ZombieData data, out Vector3 destination)
    {
        destination = default;
        float radius = data != null ? data.patrolRadius : DefaultRadius;

        for (int attempt = 0; attempt < MaxPickAttempts; attempt++)
        {
            Vector3 candidate = CandidatePoint(_home, radius, Random.insideUnitCircle);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, SampleMaxDistance, NavMesh.AllAreas))
            {
                continue;
            }

            // PathComplete matters: CalculatePath also returns true for
            // partial paths, which would walk the zombie into a wall corner
            // instead of to a real patrol point.
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, _path))
            {
                continue;
            }
            if (_path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            destination = hit.position;
            return true;
        }

        return false;
    }
}
