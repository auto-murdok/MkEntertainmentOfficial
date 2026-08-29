using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class AICharacterController : MonoBehaviour, IObserver<AICharacterActions, Vector3>
{
    [SerializeField] Subject<AICharacterActions, Vector3> _actionsSubject;
    [SerializeField] private Transform _visionHook;
    [SerializeField] private LayerMask _detectionLayerMask;
    [SerializeField] private LayerMask _ignoreLayerMask;

    private NavMeshAgent _agent;
    private Animator _animator;
    private ICommandable _commandable;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _commandable = GetComponent<ICommandable>();

        Assert.IsNotNull(_agent, "NavMeshAgent missing in " + gameObject.name);
        Assert.IsNotNull(_animator, "Animator missing in " + gameObject.name);
        Assert.IsNotNull(_visionHook, "Vision Hook missing in " + gameObject.name);
    }

    // Move the agent toward the requested world-space destination. Prefer driving
    // a state-machine entity (via ICommandable) so click-to-move reuses the same
    // FSM movement as every other AI; fall back to the raw agent otherwise.
    private void MoveToDestination(Vector3 destination)
    {
        if (_commandable != null)
        {
            _commandable.SetMoveDestination(destination);
        }
        else if (_agent != null)
        {
            _agent.SetDestination(destination);
        }
    }

    // observer logic
    public void OnNotify(AICharacterActions action, Vector3 value)
    {
        switch (action)
        {
            case AICharacterActions.MoveToDestination:
                MoveToDestination(value);
                break;
        }
    }

    private void OnEnable()
    {
        _actionsSubject.AddObserver(this);
    }

    void OnDisable()
    {
        _actionsSubject.RemoveObserver(this);
    }

    private void OnDrawGizmos()
    {
        if (_agent && _agent.hasPath)
        {
            for (int i = 0; i < _agent.path.corners.Length - 1; i++)
            {
                Debug.DrawLine(_agent.path.corners[i], _agent.path.corners[i + 1], Color.red);
            }
        }
    }
}
