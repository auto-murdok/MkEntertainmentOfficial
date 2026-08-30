// Networking seam between the player brain and the player prefab's network
// composition (NetworkedPlayerComposition). Game.Characters cannot reference
// Game.Composition (that would invert the dependency direction), so the brain
// talks to this interface and the composition implements it.
public interface IPlayerBiteRelay
{
    // False when this copy is a remote simulation: the bite interaction lands
    // wherever the zombie AI runs (the host), but the victim-side take-bite
    // FSM belongs to the victim's owner — this copy must relay instead of
    // simulating (it would fight the owner-authoritative transform).
    bool SimulatesLocally { get; }

    // Server-side: forward a bite interaction to the victim's owner peer.
    void RelayBiteFromServer(IInteractable attacker);

    // Bite state mirrored back from the owner (owner-write NetworkVariables),
    // consumed by IBiteTarget on remote copies so multi-attacker availability
    // checks (CanVictimBeBitten) stay correct.
    bool MirroredIsBitten { get; }
    IInteractable MirroredBiter { get; }
}
