#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Subscription usage across backends for the USAGE view and the topbar chip. Same
    /// shape as <see cref="ModelCatalogService"/>: providers fetch off-thread, results are
    /// posted back to the main thread, <see cref="Changed"/> fires there, and the last
    /// snapshots are cached under Library/ so numbers show instantly after domain reloads.
    /// </summary>
    public static class UsageService
    {
        private static readonly IUsageProvider[] Providers = { new ClaudeUsageProvider(), new CodexUsageProvider() };

        /// <summary>Snapshots younger than this are not re-fetched (unless forced).</summary>
        private static readonly TimeSpan MinFetchInterval = TimeSpan.FromSeconds(30);

        private static readonly object _lock = new();
        private static Dictionary<string, ProviderUsageSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private static bool _fetching;

        /// <summary>Raised on the main thread whenever any provider snapshot updates.</summary>
        public static event Action? Changed;

        public static bool IsRefreshing => _fetching;

        private static string CachePath => Path.Combine(
            MatrixSpacePaths.ProjectRoot, "Library", "com.feeder.mcp", "usage-cache.json");

        public static bool SupportsUsage(string presetId)
        {
            foreach (var provider in Providers)
            {
                if (string.Equals(provider.PresetId, presetId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static ProviderUsageSnapshot? Get(string presetId)
        {
            lock (_lock)
                return _snapshots.TryGetValue(presetId, out var snapshot) ? snapshot : null;
        }

        /// <summary>Loads the disk cache once and kicks a background refresh for stale data.</summary>
        public static void EnsureLoaded()
        {
            lock (_lock)
            {
                if (!_loaded)
                {
                    _loaded = true;
                    LoadCacheFromDisk();
                }
            }

            Refresh(force: false);
        }

        /// <summary>Fetches every provider whose snapshot is missing or older than the debounce window.</summary>
        public static void Refresh(bool force)
        {
            if (_fetching)
                return;

            var due = new List<IUsageProvider>();
            lock (_lock)
            {
                foreach (var provider in Providers)
                {
                    if (force ||
                        !_snapshots.TryGetValue(provider.PresetId, out var existing) ||
                        DateTime.UtcNow - existing.FetchedAtUtc > MinFetchInterval)
                    {
                        due.Add(provider);
                    }
                }
            }

            if (due.Count == 0)
                return;

            _fetching = true;
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var results = new List<ProviderUsageSnapshot>();
                foreach (var provider in due)
                {
                    try
                    {
                        results.Add(provider.Fetch());
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ProviderUsageSnapshot
                        {
                            PresetId = provider.PresetId,
                            Status = UsageAvailability.Error,
                            FetchedAtUtc = DateTime.UtcNow,
                            Error = ex.Message,
                        });
                    }
                }

                void Apply()
                {
                    _fetching = false;
                    lock (_lock)
                    {
                        foreach (var snapshot in results)
                            _snapshots[snapshot.PresetId] = snapshot;
                    }

                    WriteCacheToDisk();
                    Changed?.Invoke();
                }

                if (context != null)
                    context.Post(_ => Apply(), null);
                else
                    Apply();
            });
        }

        // ── Disk cache ───────────────────────────────────────────────────

        private static void LoadCacheFromDisk()
        {
            try
            {
                if (!File.Exists(CachePath))
                    return;

                var cached = JsonSerializer.Deserialize<Dictionary<string, ProviderUsageSnapshot>>(
                    File.ReadAllText(CachePath));
                if (cached != null)
                    _snapshots = new Dictionary<string, ProviderUsageSnapshot>(cached, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Corrupt cache is ignored; the next refresh rewrites it.
            }
        }

        private static void WriteCacheToDisk()
        {
            try
            {
                Dictionary<string, ProviderUsageSnapshot> copy;
                lock (_lock)
                    copy = new Dictionary<string, ProviderUsageSnapshot>(_snapshots);

                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                File.WriteAllText(CachePath, JsonSerializer.Serialize(copy));
            }
            catch (Exception)
            {
                // Cache write is best-effort.
            }
        }
    }
}
