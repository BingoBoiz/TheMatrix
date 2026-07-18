#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Feeder.MCP.Editor.MatrixSpace
{
    public enum AgentAvailability
    {
        Unknown,
        Probing,
        NotInstalled,
        Ready,
        AuthRequired,
    }

    public sealed class AgentStatusInfo
    {
        public AgentAvailability Availability = AgentAvailability.Unknown;
        public string? Version;
        public string? ResolvedPath;
    }

    /// <summary>
    /// Per-backend install/auth status shown in CONFIG. Probing runs `--version` off-thread;
    /// AuthRequired is flagged by sessions when a turn fails with an authentication error.
    /// </summary>
    public static class AgentStatusService
    {
        private static readonly Dictionary<string, AgentStatusInfo> _cache = new();
        private static readonly HashSet<string> _authRequired = new();

        /// <summary>Raised on the main thread whenever any status changes.</summary>
        public static event Action? Changed;

        public static AgentStatusInfo GetStatus(AgentBackendPreset preset)
        {
            if (!_cache.TryGetValue(preset.Id, out var info))
            {
                info = new AgentStatusInfo();
                _cache[preset.Id] = info;
                Probe(preset);
            }

            if (_authRequired.Contains(preset.Id) && info.Availability == AgentAvailability.Ready)
                return new AgentStatusInfo
                {
                    Availability = AgentAvailability.AuthRequired,
                    Version = info.Version,
                    ResolvedPath = info.ResolvedPath,
                };

            return info;
        }

        public static void Probe(AgentBackendPreset preset)
        {
            var info = _cache.TryGetValue(preset.Id, out var existing) ? existing : new AgentStatusInfo();
            _cache[preset.Id] = info;
            info.Availability = AgentAvailability.Probing;

            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var result = ProbeSync(preset);
                if (context != null)
                {
                    context.Post(_ =>
                    {
                        _cache[preset.Id] = result;
                        Changed?.Invoke();
                    }, null);
                }
                else
                {
                    _cache[preset.Id] = result;
                }
            });
        }

        /// <summary>Called by sessions when a turn fails with an authentication error.</summary>
        public static void MarkAuthRequired(string presetId)
        {
            if (_authRequired.Add(presetId))
                Changed?.Invoke();
        }

        /// <summary>Called by sessions after a successful turn.</summary>
        public static void ClearAuthRequired(string presetId)
        {
            if (_authRequired.Remove(presetId))
                Changed?.Invoke();
        }

        public static bool LooksLikeAuthError(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var lower = text!.ToLowerInvariant();
            return lower.Contains("authenticate") || lower.Contains("oauth") ||
                   lower.Contains("/login") || lower.Contains("not logged in") ||
                   lower.Contains("api key");
        }

        private static AgentStatusInfo ProbeSync(AgentBackendPreset preset)
        {
            var path = AgentBackendCatalog.ResolveExecutablePath(preset);
            if (path == null)
            {
                return new AgentStatusInfo { Availability = AgentAvailability.NotInstalled };
            }

            var version = CliPathResolver.ProbeVersion(path);
            return new AgentStatusInfo
            {
                Availability = version != null ? AgentAvailability.Ready : AgentAvailability.Unknown,
                Version = version,
                ResolvedPath = path,
            };
        }
    }
}
