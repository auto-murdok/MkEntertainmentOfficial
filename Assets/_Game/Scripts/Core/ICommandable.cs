using UnityEngine;

// Implemented by any entity that can be driven by a commanded move target
// (e.g. click-to-move). Lets locomotion controllers stay decoupled from the
// concrete state machine they happen to be controlling.
public interface ICommandable
{
    void SetMoveDestination(Vector3 destination);
    void ClearMoveDestination();
}
