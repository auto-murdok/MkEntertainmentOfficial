using System.Collections.Generic;
using UnityEngine;

public class Subject<TAction, TValue> : MonoBehaviour
{
    // a collection of all observers of this subject
    private List<IObserver<TAction, TValue>> _observers = new List<IObserver<TAction, TValue>>();

    // add the observer to the subject's collection
    public void AddObserver(IObserver<TAction, TValue> observer)
    {
        if (_observers == null)
        {
            // Editor AddComponent of generic MonoBehaviours skips field
            // initializers — lazily repair so tests and hot-reload survive.
            _observers = new List<IObserver<TAction, TValue>>();
        }
        _observers.Add(observer);
    }

    public void RemoveObserver(IObserver<TAction, TValue> observer)
    {
        if (_observers == null) return;
        _observers.Remove(observer);
    }

    // notify each observer that an event has occurred
    public void NotifyObservers(TAction action, TValue value)
    {
        if (_observers == null) return;
        // Reverse loop: observers may remove themselves (or be destroyed and
        // skipped) mid-notification without skipping or reordering the rest.
        for (int i = _observers.Count - 1; i >= 0; i--)
        {
            if (_observers[i] != null)
            {
                _observers[i].OnNotify(action, value);
            }
        }
    }
}
