#nullable enable

using UnityEditor;
using UnityEngine;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Smoke-test helpers for the Matrix Space CLI integration (no UI involved).
    /// </summary>
    public static class MatrixSpaceDebug
    {
        private static AgentSession? _pingSession;

        [MenuItem("Tools/Feeder/MCP/Debug/MatrixSpace CLI Ping", priority = 2003)]
        public static void CliPing()
        {
            var version = ClaudeCliLocator.ProbeVersion();
            if (version == null)
            {
                Debug.LogError("[MatrixSpace] claude CLI not found or --version failed. " +
                               $"Locate() = {ClaudeCliLocator.Locate() ?? "<null>"}");
                return;
            }

            Debug.Log($"[MatrixSpace] claude CLI found: {version}");

            _pingSession?.Dispose();
            var session = new AgentSession("debug-ping", "DEBUG", new ClaudeCliBackend());
            _pingSession = session;

            session.Changed += s => Debug.Log($"[MatrixSpace] state={s.State} entries={s.Transcript.Count} cost=${s.TotalCostUsd:F4}");
            session.EntryAdded += (s, i) =>
            {
                var entry = s.Transcript[i];
                Debug.Log($"[MatrixSpace] [{entry.Kind}] {entry.Text}");
            };

            session.Send("Reply with a single short sentence confirming the link is operational. Do not use any tools.");
        }

        [MenuItem("Tools/Feeder/MCP/Debug/MatrixSpace CLI Ping (dispose)", priority = 2004)]
        public static void CliPingDispose()
        {
            _pingSession?.Dispose();
            _pingSession = null;
            Debug.Log("[MatrixSpace] ping session disposed.");
        }
    }
}
