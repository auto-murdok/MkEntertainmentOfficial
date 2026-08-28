using UnityEngine;

public interface IObserver<TAction, TValue>
{
    public void OnNotify(TAction action, TValue value);
}
