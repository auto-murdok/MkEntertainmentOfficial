using UnityEngine;

public interface IFirearm
{
    public void Prepare(int clipSize, int reserveAmmo);
    public void Shoot(Vector3 mouseWorldPosition);
    public void TriggerReload();
    public void RegisterEvents(FirearmEvents fireArmEvents);
}
