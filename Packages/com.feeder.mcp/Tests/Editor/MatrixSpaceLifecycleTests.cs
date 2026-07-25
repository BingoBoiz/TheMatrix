#nullable enable

using System;
using System.IO;
using Feeder.MCP.Editor.MatrixSpace;
using Feeder.MCP.Editor.Utils;
using NUnit.Framework;
using UnityEditor;

namespace Feeder.MCP.Editor.Tests
{
    public sealed class MatrixSpaceLifecycleTests
    {
        [Test]
        public void ReloadLease_UsesOneGlobalLock_AndReleasesAfterLastAgent()
        {
            var baseline = MatrixSpaceReloadCoordinator.ActiveTurnCount;
            var generation = MatrixSpaceReloadCoordinator.LockGeneration;
            var alreadyLocked = MatrixSpaceReloadCoordinator.IsAssemblyReloadLockedByMatrix;
            ReloadLease? first = null;
            ReloadLease? second = null;
            try
            {
                first = MatrixSpaceReloadCoordinator.Acquire("test-pane-1");
                var generationAfterFirst = MatrixSpaceReloadCoordinator.LockGeneration;
                second = MatrixSpaceReloadCoordinator.Acquire("test-pane-2");

                Assert.That(generationAfterFirst, Is.EqualTo(generation + (alreadyLocked ? 0 : 1)));
                Assert.That(MatrixSpaceReloadCoordinator.LockGeneration, Is.EqualTo(generationAfterFirst));
                Assert.That(MatrixSpaceReloadCoordinator.IsAssemblyReloadLockedByMatrix, Is.True);
                Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline + 2));

                first.Dispose();
                Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline + 1));
                Assert.That(MatrixSpaceReloadCoordinator.IsAssemblyReloadLockedByMatrix, Is.True);

                second.Dispose();
                second.Dispose();
                Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline));
                Assert.That(MatrixSpaceReloadCoordinator.IsAssemblyReloadLockedByMatrix, Is.EqualTo(alreadyLocked));
            }
            finally
            {
                second?.Dispose();
                first?.Dispose();
            }
        }

        [Test]
        public void AgentSession_HoldsLeaseUntilProcessExited_NotResult()
        {
            var baseline = MatrixSpaceReloadCoordinator.ActiveTurnCount;
            var backend = new FakeBackend();
            using var session = new AgentSession("lease-test", "LEASE TEST", backend);

            session.Send("probe");
            Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline + 1));

            backend.Emit(new AgentEvent(AgentEventKind.Result));
            Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline + 1));

            backend.Exit();
            backend.Exit();
            Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline));
        }

        [Test]
        public void DeferredScriptNotification_IsScheduledWithoutWaitingForCompilation()
        {
            const string requestId = "matrix-space-deferred-notification-test";
            var notificationKey = "Feeder_MCP_PendingNotification_" + requestId;
            using var lease = MatrixSpaceReloadCoordinator.Acquire("notification-test");
            try
            {
                ScriptUtils.SchedulePostCompilationNotification(requestId, "Assets/Test.cs", "Script update");
                var scheduledField = typeof(ScriptUtils).GetField("_processPendingScheduled",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(scheduledField, Is.Not.Null);
                Assert.That((bool)scheduledField!.GetValue(null), Is.True);
            }
            finally
            {
                SessionState.EraseString(notificationKey);
                SessionState.EraseString("Feeder_MCP_PendingNotificationKeys");
            }
        }

        [Test]
        public void AgentSession_StartFailureAndDispose_DoNotLeakLease()
        {
            var baseline = MatrixSpaceReloadCoordinator.ActiveTurnCount;
            var failingBackend = new FakeBackend { ThrowOnSend = true };
            using (var failed = new AgentSession("start-failure", "START FAILURE", failingBackend))
            {
                failed.Send("probe");
                Assert.That(failed.State, Is.EqualTo(AgentSessionState.Error));
                Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline));
            }

            var runningBackend = new FakeBackend();
            var running = new AgentSession("dispose-test", "DISPOSE TEST", runningBackend);
            running.Send("probe");
            Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline + 1));
            running.Dispose();
            running.Dispose();
            Assert.That(MatrixSpaceReloadCoordinator.ActiveTurnCount, Is.EqualTo(baseline));
        }

        [Test]
        public void SnapshotRoundTrip_PreservesTranscriptUsageAndSessionId()
        {
            var started = new DateTime(2026, 7, 19, 3, 4, 5, DateTimeKind.Utc);
            var completed = started.AddSeconds(7);
            var sourceEntry = new TranscriptEntry(TranscriptEntryKind.Assistant, "restored answer", isComplete: true)
            {
                Timestamp = started.ToLocalTime(),
                CompletedAt = completed.ToLocalTime(),
            };
            var transcript = new MatrixSpaceSessionStore.TranscriptSnapshot();
            transcript.Capture(sourceEntry);

            var record = new MatrixSpaceSessionStore.PaneRecord
            {
                PaneId = "round-trip",
                DisplayName = "ROUND TRIP",
                BackendId = "codex",
                BackendSessionId = "session-123",
                TotalCostUsd = "1.2500",
                TotalInputTokens = 11,
                TotalOutputTokens = 22,
                TotalCacheReadTokens = 33,
                TotalCacheCreationTokens = 44,
                LastState = (int)AgentSessionState.WaitingInput,
            };
            record.Transcript.Add(transcript);

            var backend = new FakeBackend { SessionId = record.BackendSessionId };
            using var restored = new AgentSession(record.PaneId, record.DisplayName, backend, record.BackendId, record);

            Assert.That(restored.Transcript.Count, Is.EqualTo(1));
            Assert.That(restored.Transcript[0].Kind, Is.EqualTo(TranscriptEntryKind.Assistant));
            Assert.That(restored.Transcript[0].Text, Is.EqualTo("restored answer"));
            Assert.That(restored.Transcript[0].Timestamp.ToUniversalTime().Ticks, Is.EqualTo(started.Ticks));
            Assert.That(restored.Transcript[0].CompletedAt!.Value.ToUniversalTime().Ticks, Is.EqualTo(completed.Ticks));
            Assert.That(restored.TotalCostUsd, Is.EqualTo(1.25m));
            Assert.That(restored.TotalInputTokens, Is.EqualTo(11));
            Assert.That(restored.TotalOutputTokens, Is.EqualTo(22));
            Assert.That(restored.TotalCacheReadTokens, Is.EqualTo(33));
            Assert.That(restored.TotalCacheCreationTokens, Is.EqualTo(44));
            Assert.That(restored.Backend.SessionId, Is.EqualTo("session-123"));
            Assert.That(restored.State, Is.EqualTo(AgentSessionState.WaitingInput));
        }

        [Test]
        public void RecoveryParsers_ImportOnlyAllowedConversationContent()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "matrix-space-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var codexPath = Path.Combine(tempRoot, "codex.jsonl");
                File.WriteAllLines(codexPath, new[]
                {
                    "{\"timestamp\":\"2026-07-19T00:00:00Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"role\":\"developer\",\"content\":[{\"type\":\"input_text\",\"text\":\"hidden developer\"}]}}",
                    "{\"timestamp\":\"2026-07-19T00:00:01Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":\"<environment_context>hidden envelope</environment_context>\"}]}}",
                    "{\"timestamp\":\"2026-07-19T00:00:02Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":\"real user\"}]}}",
                    "{\"timestamp\":\"2026-07-19T00:00:03Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"real answer\"}]}}",
                    "{\"timestamp\":\"2026-07-19T00:00:04Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"custom_tool_call\",\"name\":\"tests-run\"}}",
                    "{\"timestamp\":\"2026-07-19T00:00:05Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"custom_tool_call_output\",\"output\":\"raw secret payload\"}}",
                });

                var codex = BackendTranscriptRecovery.ReadCodex(codexPath);
                Assert.That(codex.ConvertAll(entry => entry.Text), Is.EqualTo(new[]
                {
                    "real user", "real answer", "> executing: tests-run",
                }));

                var claudePath = Path.Combine(tempRoot, "claude.jsonl");
                File.WriteAllLines(claudePath, new[]
                {
                    "{\"type\":\"user\",\"timestamp\":\"2026-07-19T00:00:00Z\",\"message\":{\"content\":\"claude user\"}}",
                    "{\"type\":\"assistant\",\"timestamp\":\"2026-07-19T00:00:01Z\",\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":\"thought\"},{\"type\":\"text\",\"text\":\"claude answer\"},{\"type\":\"tool_use\",\"name\":\"script-read\",\"input\":{\"secret\":true}}]}}",
                    "{\"type\":\"user\",\"timestamp\":\"2026-07-19T00:00:02Z\",\"toolUseResult\":{\"raw\":\"hidden\"},\"message\":{\"content\":\"raw tool result\"}}",
                });

                var claude = BackendTranscriptRecovery.ReadClaude(claudePath);
                Assert.That(claude.ConvertAll(entry => entry.Text), Is.EqualTo(new[]
                {
                    "claude user", "thought", "claude answer", "> executing: script-read",
                }));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        private sealed class FakeBackend : IAgentBackend
        {
            public string Name => "Fake";
            public bool IsRunning { get; private set; }
            public bool ThrowOnSend { get; set; }
            public string? SessionId { get; set; }
            public string? ModelOverride { get; set; }
            public string? ModeOverride { get; set; }
            public string? EffortOverride { get; set; }
            public string? AgentLabel { get; set; }
            public event Action<AgentEvent>? EventReceived;

            public void SendPrompt(string prompt)
            {
                if (ThrowOnSend)
                    throw new InvalidOperationException("start failed");
                IsRunning = true;
            }

            public void Cancel()
            {
                if (!IsRunning)
                    return;
                Exit();
            }

            public void Dispose() => IsRunning = false;

            public void Emit(AgentEvent evt) => EventReceived?.Invoke(evt);

            public void Exit()
            {
                IsRunning = false;
                Emit(new AgentEvent(AgentEventKind.ProcessExited));
            }
        }
    }
}
