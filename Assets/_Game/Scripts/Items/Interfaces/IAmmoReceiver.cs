// Anything that can receive reserve ammunition (ammo pickups, future
// inventory systems). Implemented by Weapon so pickups never need to know
// the concrete firearm type.
public interface IAmmoReceiver
{
    public void AddReserveAmmo(int amount);
}
