#nullable enable

using System;
using Feeder.MCP.Editor.UI;
using NUnit.Framework;

namespace Feeder.MCP.Editor.Tests
{
    public sealed class BridgeStatusTests
    {
        [TestCase(false, false, false, McpServerStatus.Stopped, null, BridgePhase.Offline)]
        [TestCase(false, false, false, McpServerStatus.Running, null, BridgePhase.Offline)]
        [TestCase(true, false, false, McpServerStatus.Stopped, null, BridgePhase.Linking)]
        [TestCase(false, false, true, McpServerStatus.Stopped, null, BridgePhase.Linking)]
        [TestCase(false, false, false, McpServerStatus.Installing, null, BridgePhase.Linking)]
        [TestCase(false, false, false, McpServerStatus.Starting, null, BridgePhase.Linking)]
        [TestCase(true, true, false, McpServerStatus.Running, null, BridgePhase.Online)]
        [TestCase(false, true, false, McpServerStatus.External, null, BridgePhase.Online)]
        [TestCase(true, true, true, McpServerStatus.Running, "install failed", BridgePhase.Fault)]
        [TestCase(false, false, false, McpServerStatus.Stopped, "install failed", BridgePhase.Fault)]
        public void Resolve_ReturnsExpectedPhase(bool keep, bool connected, bool connecting, McpServerStatus server, string? error, BridgePhase expected)
        {
            Assert.AreEqual(expected, BridgeStatus.Resolve(keep, connected, connecting, server, error));
        }

        [Test]
        public void Resolve_EmptyInstallError_IsNotAFault()
        {
            Assert.AreEqual(BridgePhase.Offline, BridgeStatus.Resolve(false, false, false, McpServerStatus.Stopped, string.Empty));
        }

        [Test]
        public void EveryPhase_HasAWordAndAnEnergyInRange()
        {
            foreach (BridgePhase phase in Enum.GetValues(typeof(BridgePhase)))
            {
                var word = BridgeStatus.Word(phase);
                Assert.IsNotEmpty(word);
                Assert.AreEqual(word.ToUpperInvariant(), word);
                Assert.That(BridgeStatus.RainEnergy(phase), Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void RainEnergy_RisesFromOfflineToOnline()
        {
            Assert.Less(BridgeStatus.RainEnergy(BridgePhase.Offline), BridgeStatus.RainEnergy(BridgePhase.Linking));
            Assert.Less(BridgeStatus.RainEnergy(BridgePhase.Linking), BridgeStatus.RainEnergy(BridgePhase.Online));
            Assert.Less(BridgeStatus.RainEnergy(BridgePhase.Fault), BridgeStatus.RainEnergy(BridgePhase.Online));
        }

        [Test]
        public void Word_UnknownPhase_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BridgeStatus.Word((BridgePhase)99));
        }
    }
}
