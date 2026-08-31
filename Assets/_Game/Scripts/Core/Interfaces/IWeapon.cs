using UnityEngine;

/// <summary>
/// Core abstraction for equippable weapons. Lives in Game.Core so
/// Characters and UI never need a direct Game.Items reference — the
/// composition root injects the concrete Weapon. Mirrors the freeCodeCamp
/// 2026 modular packages pattern (feature asmdefs depend only on Core).
/// </summary>
public interface IWeapon
{
    float recoilForce { get; }
    int clipSize { get; }
    int maxClipSize { get; }
    int reserveAmmo { get; }

    void TriggerShoot(Vector3 aimPosition);
    void TriggerReload();
    void AddReserveAmmo(int amount);
}
