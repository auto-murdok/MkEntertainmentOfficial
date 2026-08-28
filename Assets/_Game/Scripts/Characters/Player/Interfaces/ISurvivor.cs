using UnityEngine;

public interface ISurvivor {
    public void TakeDamage();
    public void RecoverControl();
    public Vector3 TargetPosition { get;}
}