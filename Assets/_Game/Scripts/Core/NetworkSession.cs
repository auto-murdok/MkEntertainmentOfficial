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
    public const string ServerAddress = "127.0.0.1";
    public const ushort ServerPort = 7777;
}
