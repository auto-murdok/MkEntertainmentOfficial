using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    internal class TestSubject : Subject<string, int> { }

    public class SubjectPlayTests
    {
        private GameObject _host;
        private Subject<string, int> _subject;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SubjectHost");
            _subject = _host.AddComponent<TestSubject>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.Destroy(obj);
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            _cleanup.Add(obj);
            return obj;
        }

        [Test]
        public void AddObserver_NotifiesObserver()
        {
            var observer = new RecordingObserver();
            _subject.AddObserver(observer);
            _subject.NotifyObservers("fire", 7);
            Assert.AreEqual(1, observer.Actions.Count);
            Assert.AreEqual("fire", observer.Actions[0]);
            Assert.AreEqual(7, observer.Values[0]);
        }

        [Test]
        public void NotifyObservers_MultipleObservers_LastAddedIsNotifiedFirst()
        {
            var first = new RecordingObserver();
            var second = new RecordingObserver();
            _subject.AddObserver(first);
            _subject.AddObserver(second);
            _subject.NotifyObservers("hit", 1);
            Assert.AreEqual(1, first.Actions.Count);
            Assert.AreEqual(1, second.Actions.Count);
        }

        [Test]
        public void RemoveObserver_StopsNotifications()
        {
            var observer = new RecordingObserver();
            _subject.AddObserver(observer);
            _subject.RemoveObserver(observer);
            _subject.NotifyObservers("hit", 1);
            Assert.AreEqual(0, observer.Actions.Count);
        }

        [Test]
        public void RemoveObserver_NotSubscribed_NoThrow()
        {
            Assert.DoesNotThrow(() => _subject.RemoveObserver(new RecordingObserver()));
        }

        [Test]
        public void AddObserver_DoesNotDedupe_ObserverNotifiedTwice()
        {
            var observer = new RecordingObserver();
            _subject.AddObserver(observer);
            _subject.AddObserver(observer);
            _subject.NotifyObservers("hit", 1);
            Assert.AreEqual(2, observer.Actions.Count);
        }

        [Test]
        public void NotifyObservers_DestroyedObserver_IsSkipped()
        {
            var host = Track(new GameObject("ObserverHost"));
            var destroyed = host.AddComponent<RecordingMonoObserver>();
            var healthy = Track(new GameObject("HealthyHost")).AddComponent<RecordingMonoObserver>();
            _subject.AddObserver(destroyed);
            _subject.AddObserver(healthy);

            Object.DestroyImmediate(host);
            Assert.DoesNotThrow(() => _subject.NotifyObservers("hit", 1));
            Assert.AreEqual(1, healthy.NotifyCount);
        }

        [Test]
        public void NotifyObservers_ObserverRemovesItselfDuringNotify_DoesNotSkipOthers()
        {
            var selfRemover = new RecordingObserver();
            var later = new RecordingObserver();
            _subject.AddObserver(later);
            _subject.AddObserver(selfRemover);
            selfRemover.SubjectToDetach = _subject;

            _subject.NotifyObservers("hit", 1);
            Assert.AreEqual(1, later.Actions.Count);
            _subject.NotifyObservers("hit", 2);
            Assert.AreEqual(2, later.Actions.Count);
            Assert.AreEqual(1, selfRemover.Actions.Count);
        }

        [Test]
        public void CharacterUIController_UpdateUI_NotifiesAimElement()
        {
            var uiHost = Track(new GameObject("UIHost"));
            var controller = uiHost.AddComponent<CharacterUIController>();
            var observer = new RecordingUIObserver();
            controller.AddObserver(observer);
            controller.UpdateUI(CharacterUIContext.CreateAimUI(true));
            Assert.AreEqual(1, observer.Elements.Count);
            Assert.AreEqual(CharacterUIElement.AimUI, observer.Elements[0]);
            Assert.IsTrue(observer.Contexts[0].displayCrosshair);
        }
    }
}
