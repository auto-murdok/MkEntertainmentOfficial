using UnityEngine;

public interface IFireArm
{
    public void Prepare(int clipSize);
    public void Shoot(Vector3 mouseWorldPosition);
    public void TriggerReload();
    public void RegisterEvents(FireArmEvents fireArmEvents);
}