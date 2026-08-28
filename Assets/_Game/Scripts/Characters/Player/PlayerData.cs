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

    public float moveSpeed => _moveSpeed;
    public float sprintSpeed => _sprintSpeed;
    public float aimTurnSpeed => _aimTurnSpeed;
    public float reloadDuration => _reloadDuration;
}
