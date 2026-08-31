using System;

/// <summary>
/// Health data exposed to UI without coupling UI to CharacterBrain.
/// Game.UI depends only on this Core contract.
/// </summary>
public interface IHealthSource
{
    float remainingHitPoints { get; }
    float maxHitPoints { get; }
    event Action<float> Damaged;
    event Action Died;
}
