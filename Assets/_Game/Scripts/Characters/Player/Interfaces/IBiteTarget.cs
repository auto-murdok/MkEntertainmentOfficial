// Implemented by actors that can be grabbed and bitten. A bite is an exclusive
// grab attack: while a victim is already pinned by another attacker it reports
// canBeBitten == false so AI attackers fall back to non-exclusive attacks
// (e.g. the zombie right-hand swing) instead of biting air. currentBiter is the
// attacker currently holding the pin (null when free) so the pinning attacker
// itself can still proceed with its bite — the victim marks itself attacked
// synchronously inside the bite interaction, before the attacker's own side of
// the interaction is notified.
public interface IBiteTarget
{
    public bool canBeBitten { get; }
    public IInteractable currentBiter { get; }
}
