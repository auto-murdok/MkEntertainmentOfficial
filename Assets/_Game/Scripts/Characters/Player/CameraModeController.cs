using Cinemachine;
using UnityEngine;

public enum CameraMode
{
    Follow,
    Aim,
}

// The single owner of the local player's camera mode.
//
// Replaces the old arrangement where the Aim virtual camera's GameObject
// was SetActive-toggled as a side effect of a UI notification (PlayerCoreUI
// reacting to the Aim state's crosshair event): camera mode is gameplay
// state, so it is driven directly by the character state machine — the
// semantic owner of Aiming — and expressed through Cinemachine's own
// selection mechanism, priority, with both vcams kept active so the Brain
// blends between them and the standby camera's state stays warm.
//
// Deliberately NOT a state machine of its own: two modes today (Follow /
// Aim) and the roadmap (sprint FOV, death cam) are enum cases + vcams
// here. Revisit a formal camera FSM only when modes start competing
// (5-6+, multiple independent requesters, per-mode continuous behaviour).
//
// Local-only: PlayerRigging creates this for the owner's rig; remote
// characters never get one and nothing here is networked.
public class CameraModeController : MonoBehaviour
{
    // The camera prefabs author Follow at 10 and Aim at 20; the controller
    // keeps those two values and swaps which camera holds the high one.
    public const int LivePriority = 20;
    public const int StandbyPriority = 10;

    private CinemachineVirtualCamera _followCamera;
    private CinemachineVirtualCamera _aimCamera;
    private StateMachine<CharacterState, CharacterStateContext> _stateMachine;

    public CameraMode CurrentMode { get; private set; } = CameraMode.Follow;

    // Wire the controller to the character's state machine and the two
    // vcams, and apply the mode matching the state machine's current state.
    public void Wire(StateMachine<CharacterState, CharacterStateContext> stateMachine,
                     CinemachineVirtualCamera followCamera,
                     CinemachineVirtualCamera aimCamera)
    {
        Unsubscribe();

        _stateMachine = stateMachine;
        _followCamera = followCamera;
        _aimCamera = aimCamera;

        if (_aimCamera != null)
        {
            // The aim camera shares the follow target: keep its internal
            // state tracking while it is on standby so entering Aim does
            // not converge from a stale pose on the first transition.
            _aimCamera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        }

        // Gold-standard aim feel: look sensitivity scales with the FOV
        // ratio of the two cameras (focal-length scaling — see
        // AimSensitivity), so on-screen tracking speed is the same at hip
        // and while aiming instead of speeding up with the zoom.
        if (_followCamera != null && _aimCamera != null && stateMachine is CharacterLocomotion locomotion)
        {
            locomotion.SetAimLookSensitivityScale(
                AimSensitivity.FovRatioScale(_followCamera.m_Lens.FieldOfView, _aimCamera.m_Lens.FieldOfView));
        }

        if (_stateMachine != null)
        {
            _stateMachine.OnStateChanged += HandleStateChanged;
            HandleStateChanged(_stateMachine.CurrentStateName);
        }
        else
        {
            SetMode(CameraMode.Follow);
        }
    }

    public void SetMode(CameraMode mode)
    {
        CurrentMode = mode;

        if (_followCamera != null)
        {
            _followCamera.Priority = mode == CameraMode.Follow ? LivePriority : StandbyPriority;
        }

        if (_aimCamera != null)
        {
            _aimCamera.Priority = mode == CameraMode.Aim ? LivePriority : StandbyPriority;
        }
    }

    private void HandleStateChanged(CharacterState state)
    {
        // Aim camera <=> the character is in the Aiming state — the exact
        // mapping the old UI-observer SetActive path implemented (entering
        // Reloading returned to the follow camera, via AimUI off).
        SetMode(state == CharacterState.Aiming ? CameraMode.Aim : CameraMode.Follow);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_stateMachine != null)
        {
            _stateMachine.OnStateChanged -= HandleStateChanged;
            _stateMachine = null;
        }
    }
}
