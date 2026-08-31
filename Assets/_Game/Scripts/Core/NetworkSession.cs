using UnityEngine;

/// <summary>
/// Which network session role the next NetworkedCombatArena load should take.
/// The main menu sets it before transitioning (Host/Join buttons); the default
/// Auto falls back to the command line (-mlclient joins, otherwise host) so
/// automated builds keep working unchanged.
/// </summary>
public enum NetworkSessionMode
{
    Auto,
    Host,
    Client,
}

public static class NetworkSession
{
    // Plain mutable holder (no service-locator shape): the menu writes it,
    // NetworkArenaBootstrap consumes it once per scene load.
    public static NetworkSessionMode desiredMode = NetworkSessionMode.Auto;

    // Everything is local for now: single dev box, loopback only.
    // Kept as const for source compatibility but CLI overrides are applied
    // via the mutable Override* fields (GameCliArgs --address/--port/--connect).
    public const string ServerAddress = "127.0.0.1";
    public const ushort ServerPort = 7777;

    // CLI-mutable overrides (set by GameCliBootstrap). When null, fall back to const.
    public static string OverrideAddress;
    public static ushort? OverridePort;

    public static string EffectiveAddress => string.IsNullOrEmpty(OverrideAddress) ? ServerAddress : OverrideAddress;
    public static ushort EffectivePort => OverridePort ?? ServerPort;

    /// <summary>Reset mutable state (tests / scene reloads).</summary>
    public static void ResetOverrides()
    {
        desiredMode = NetworkSessionMode.Auto;
        OverrideAddress = null;
        OverridePort = null;
    }
}
