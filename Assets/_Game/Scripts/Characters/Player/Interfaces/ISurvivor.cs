using UnityEngine;

public interface ISurvivor {
    public void TakeDamage(float amount);
    public Vector3 TargetPosition { get;}
}