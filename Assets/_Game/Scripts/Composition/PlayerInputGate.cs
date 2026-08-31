using UnityEngine;
using UnityEngine.InputSystem;

// Gates player input for full-screen overlays (pause menu, game-over screen).
// Disabling the PlayerInput stops new events; zeroing the locomotion inputs
// stops the last held values (movement/aim/sprint) from persisting. The
// CharacterBrain additionally re-evaluates sprint/aim from the live action
// state each frame, so this gate also heals event-latched flags.
public class PlayerInputGate : MonoBehaviour, IPlayerInputGate
{
    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;

    public void SetInputEnabled(bool enabled)
    {
        // Lazily resolved: this gate lives on the composition root, while the
        // player and its InputHandler are spawned (host) or network-spawned
        // (client) around it.
        if (_locomotion == null)
        {
            _locomotion = FindFirstObjectByType<CharacterLocomotion>();
        }
        if (_playerInput == null)
        {
            _playerInput = FindFirstObjectByType<PlayerInput>();
        }

        if (_playerInput != null)
        {
            _playerInput.enabled = enabled;
        }

        if (!enabled && _locomotion != null)
        {
            _locomotion.setMovementInput(Vector2.zero);
            _locomotion.setLookInput(Vector2.zero, string.Empty);
            _locomotion.setIsAiming(false);
            _locomotion.setIsRunning(false);
        }
    }
}
