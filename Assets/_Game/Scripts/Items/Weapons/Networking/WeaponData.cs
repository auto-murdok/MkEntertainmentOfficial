using UnityEngine;

// Designer-facing wrapper around WeaponStats so a weapon's numbers live in
// one asset (the SO-architecture pattern this project already uses for
// PlayerData, ZombieData and the ItemCatalog). NetworkedShooter references
// the asset; when none is assigned it falls back to WeaponStats.Handgun, so
// tests and tool scenes keep working with the shipped tuning.
[CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] private WeaponStats _stats = WeaponStats.Handgun;

    public WeaponStats Stats => _stats;
}
