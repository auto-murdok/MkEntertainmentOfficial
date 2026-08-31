// Implemented by the player's networking/composition layer
// (NetworkedPlayerComposition-attached PlayerInputGate). Lets Core-side game
// flow (GameStateManager game-over) gate gameplay input without Core
// referencing Game.Composition or Game.Characters.
public interface IPlayerInputGate
{
    // Disables gameplay input (movement, look, aim, sprint, shoot) and zeroes
    // held values; re-enabling restores action processing.
    void SetInputEnabled(bool enabled);
}
