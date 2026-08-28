using System.Collections.Generic;
using UnityEngine;

public class Subject<EGenericEnum, EGenericValue> : MonoBehaviour
{
    // a collection of all observers of this subject
    private List<IObserver<EGenericEnum, EGenericValue>> _observers = new List<IObserver<EGenericEnum, EGenericValue>>();

    // add the observer to the subject's collection
    public void AddObserver(IObserver<EGenericEnum, EGenericValue> observer)
    {
        _observers.Add(observer);
    }

    public void RemoveObserver(IObserver<EGenericEnum, EGenericValue> observer)
    {
        _observers.Remove(observer);
    }

    // notify each observer that an event has ocurred
    public void NotifyObservers(EGenericEnum action, EGenericValue value)
    {
        _observers.ForEach((_observer) => {
            _observer.OnNotify(action, value);
        });
    }
}
