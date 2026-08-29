using System.Collections.Generic;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    public class RecordingObserver : IObserver<string, int>
    {
        public readonly List<string> Actions = new List<string>();
        public readonly List<int> Values = new List<int>();
        public Subject<string, int> SubjectToDetach;

        public void OnNotify(string action, int value)
        {
            Actions.Add(action);
            Values.Add(value);
            if (SubjectToDetach != null)
            {
                SubjectToDetach.RemoveObserver(this);
                SubjectToDetach = null;
            }
        }
    }

    public class RecordingMonoObserver : MonoBehaviour, IObserver<string, int>
    {
        public int NotifyCount;

        public void OnNotify(string action, int value)
        {
            NotifyCount++;
        }
    }

    public class RecordingUIObserver : IObserver<CharacterUIElement, CharacterUIContext>
    {
        public readonly List<CharacterUIElement> Elements = new List<CharacterUIElement>();
        public readonly List<CharacterUIContext> Contexts = new List<CharacterUIContext>();

        public void OnNotify(CharacterUIElement element, CharacterUIContext context)
        {
            Elements.Add(element);
            Contexts.Add(context);
        }
    }

    public class StubInteractable : IInteractable
    {
        private readonly Transform _transform;

        public StubInteractable(int id, Transform transform = null)
        {
            this.id = id;
            _transform = transform;
        }

        public int id { get; }
        public Vector3 position => _transform != null ? _transform.position : Vector3.zero;
        public Transform victimHook => _transform;
        public bool isPreparing => false;
        public int InteractionCount { get; set; }

        public void OnExternalInteraction(IInteractable interactable)
        {
            InteractionCount++;
        }
    }
}
