using UnityEngine;

// Data-driven configuration for the player actor, mirroring ZombieData. Lets the
// PlayerCore prefab be turned into reusable variants (different speeds, etc.)
// without touching code. Assign an asset on CharacterLocomotion.
[CreateAssetMenu(fileName = "NewPlayerData", menuName = "Player/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Locomotion")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _sprintSpeed = 4f;

    [Header("Combat")]
    [SerializeField] private float _aimTurnSpeed = 1f;
    [SerializeField] private float _reloadDuration = 2f;
    [SerializeField] private float _takeBiteDuration = 3f;

    [Header("Health")]
    [Tooltip("Seconds without taking damage before passive regeneration starts.")]
    [SerializeField] private float _healthRegenDelay = 5f;
    [Tooltip("Hit points regenerated per second once the delay has elapsed.")]
    [SerializeField] private float _healthRegenRate = 5f;

    public float moveSpeed => _moveSpeed;
    public float sprintSpeed => _sprintSpeed;
    public float aimTurnSpeed => _aimTurnSpeed;
    public float reloadDuration => _reloadDuration;
    public float takeBiteDuration => _takeBiteDuration;
    public float healthRegenDelay => _healthRegenDelay;
    public float healthRegenRate => _healthRegenRate;
}
