using UnityEngine;

public interface ISurvivor {
    public void TakeDamage(float amount);
    public void RecoverControl();
    public Vector3 TargetPosition { get;}
}