using System.Collections.Generic;
using UnityEngine;

public class Subject<TAction, TValue> : MonoBehaviour
{
    // a collection of all observers of this subject
    private List<IObserver<TAction, TValue>> _observers = new List<IObserver<TAction, TValue>>();

    // add the observer to the subject's collection
    public void AddObserver(IObserver<TAction, TValue> observer)
    {
        _observers.Add(observer);
    }

    public void RemoveObserver(IObserver<TAction, TValue> observer)
    {
        _observers.Remove(observer);
    }

    // notify each observer that an event has occurred
    public void NotifyObservers(TAction action, TValue value)
    {
        _observers.ForEach((observer) =>
        {
            observer.OnNotify(action, value);
        });
    }
}
