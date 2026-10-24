using Unity.Netcode;
using UnityEngine;

// One shot, as reported by the firing client. This replaces the old damage
// report (NetworkedDamageRelay carried a bare float the server applied on
// trust): the client now reports INTENT — which weapon, from where, toward
// what, at which network tick — and the server derives everything else
// (damage, spread, outcome) from its own WeaponStats. Nothing in this
// struct is believed until ShotValidator and the rewound raycast accept it.
public struct ShotRequest : INetworkSerializable
{
    public int WeaponId;
    // Per-shooter monotonically increasing id; deduplicates the confirmed-shot
    // broadcast for the owner (who already played predicted FX) and orders
    // the seed stream.
    public uint ShotId;
    // Muzzle world position claimed by the shooter (sanity-checked against
    // the server's own copy of the gun, never trusted for the trace itself —
    // the trace starts from the server's muzzle estimate blended to this
    // origin only after the tolerance check passes).
    public Vector3 Origin;
    public Vector3 Direction;
    // NetworkManager.LocalTime.Tick on the firing peer. The server rewinds
    // its hitbox history to this tick: the world as the shooter saw it.
    public int ClientTick;
    // Per-shot seed: every peer regenerates the same spread pattern from it
    // locally, so pellet directions never travel on the wire.
    public uint Seed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref WeaponId);
        serializer.SerializeValue(ref ShotId);
        serializer.SerializeValue(ref Origin);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref ClientTick);
        serializer.SerializeValue(ref Seed);
    }
}
