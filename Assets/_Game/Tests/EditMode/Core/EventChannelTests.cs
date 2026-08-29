using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // Typed SO event channels (Unity event-channel pattern): producers Raise,
    // consumers subscribe — with no knowledge of each other.
    public class EventChannelTests
    {
        [Test]
        public void VoidChannel_Raise_NotifiesSubscriber()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            int raised = 0;
            channel.OnRaised += () => raised++;

            channel.Raise();

            Assert.AreEqual(1, raised);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void VoidChannel_Unsubscribe_StopsNotifications()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            int raised = 0;
            System.Action handler = () => raised++;
            channel.OnRaised += handler;

            channel.Raise();
            channel.OnRaised -= handler;
            channel.Raise();

            Assert.AreEqual(1, raised);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void VoidChannel_NoSubscribers_DoesNotThrow()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            Assert.DoesNotThrow(() => channel.Raise());
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void BoolChannel_Raise_PassesValueToAllSubscribers()
        {
            var channel = ScriptableObject.CreateInstance<BoolEventChannel>();
            var received = new List<bool>();
            channel.OnRaised += received.Add;
            channel.OnRaised += received.Add;

            channel.Raise(true);
            channel.Raise(false);

            Assert.AreEqual(new[] { true, true, false, false }, received);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void BoolChannel_Unsubscribe_StopsNotifications()
        {
            var channel = ScriptableObject.CreateInstance<BoolEventChannel>();
            var received = new List<bool>();
            System.Action<bool> handler = received.Add;
            channel.OnRaised += handler;

            channel.Raise(true);
            channel.OnRaised -= handler;
            channel.Raise(false);

            Assert.AreEqual(1, received.Count);
            Assert.IsTrue(received[0]);
            Object.DestroyImmediate(channel);
        }
    }
}
