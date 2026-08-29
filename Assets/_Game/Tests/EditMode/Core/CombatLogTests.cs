using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    public class CombatLogTests
    {
        private GameObject _victim;
        private readonly string[] _buffer = new string[16];

        [TearDown]
        public void TearDown()
        {
            if (_victim != null) UnityEngine.Object.DestroyImmediate(_victim);
        }

        [Test]
        public void CopyRecent_ReturnsAtMostCapacityEntries()
        {
            string message = "CapacityCheck" + Guid.NewGuid().ToString("N");
            CombatLog.ReportImpact(message);
            int written = CombatLog.CopyRecent(_buffer);
            Assert.LessOrEqual(written, 8);
            StringAssert.Contains(message, string.Join("|", _buffer, 0, written));
        }

        [Test]
        public void ReportImpact_AppendsEntryContainingMessage()
        {
            string message = "TestImpact" + Guid.NewGuid().ToString("N");
            CombatLog.ReportImpact(message);
            int written = CombatLog.CopyRecent(_buffer);
            Assert.Greater(written, 0);
            StringAssert.Contains(message, _buffer[written - 1]);
        }

        [Test]
        public void ReportDamage_NamesVictimAndAmount()
        {
            _victim = new GameObject("VictimA");
            CombatLog.ReportDamage(25f, 75f, _victim);
            int written = CombatLog.CopyRecent(_buffer);
            string entry = _buffer[written - 1];
            StringAssert.Contains("VictimA", entry);
            StringAssert.Contains("took", entry);
        }

        [Test]
        public void ReportDamage_DestroyedVictim_PrintsPlaceholder()
        {
            CombatLog.ReportDamage(5f, 95f, null);
            int written = CombatLog.CopyRecent(_buffer);
            StringAssert.Contains("<destroyed>", _buffer[written - 1]);
        }

        [Test]
        public void BeginSource_ScopesSourceNameDuringReport()
        {
            _victim = new GameObject("ScopedVictim");
            string marker = "ScopedSource" + Guid.NewGuid().ToString("N");
            using (CombatLog.BeginSource(marker))
            {
                CombatLog.ReportDamage(10f, 90f, _victim);
            }
            int written = CombatLog.CopyRecent(_buffer);
            StringAssert.Contains(marker, _buffer[written - 1]);
        }

        [Test]
        public void BeginSource_RestoresPreviousSourceOnDispose()
        {
            _victim = new GameObject("NestedVictim");
            string outer = "OuterSrc" + Guid.NewGuid().ToString("N");
            string inner = "InnerSrc" + Guid.NewGuid().ToString("N");
            using (CombatLog.BeginSource(outer))
            {
                using (CombatLog.BeginSource(inner))
                {
                    CombatLog.ReportDamage(1f, 99f, _victim);
                }
                CombatLog.ReportDamage(2f, 97f, _victim);
            }
            int written = CombatLog.CopyRecent(_buffer);
            StringAssert.Contains(inner, _buffer[written - 2]);
            StringAssert.Contains(outer, _buffer[written - 1]);
        }

        [Test]
        public void RingBuffer_OverCapacity_KeepsMostRecentEntries()
        {
            string first = "First" + Guid.NewGuid().ToString("N");
            string last = "Last" + Guid.NewGuid().ToString("N");
            CombatLog.ReportImpact(first);
            for (int i = 0; i < 20; i++)
            {
                CombatLog.ReportImpact("fill" + i);
            }
            CombatLog.ReportImpact(last);

            int written = CombatLog.CopyRecent(_buffer);
            Assert.LessOrEqual(written, 8);
            StringAssert.Contains(last, _buffer[written - 1]);
            StringAssert.DoesNotContain(first, _buffer[0]);
        }

        [Test]
        public void CopyRecent_SmallBuffer_FillsExactlyBufferSizeSlots()
        {
            CombatLog.ReportImpact("SmallBuf" + Guid.NewGuid().ToString("N"));
            var small = new string[1];
            int written = CombatLog.CopyRecent(small);
            Assert.AreEqual(1, written);
            Assert.IsNotNull(small[0]);
        }

        [Test]
        public void ReportDamage_UnknownSource_DefaultsToUnknown()
        {
            _victim = new GameObject("UnknownSrcVictim");
            CombatLog.ReportDamage(3f, 97f, _victim);
            int written = CombatLog.CopyRecent(_buffer);
            StringAssert.Contains("unknown", _buffer[written - 1]);
        }
    }
}
