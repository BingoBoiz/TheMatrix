#nullable enable

using System;
using System.Collections.Generic;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// USAGE view: one card per backend preset showing subscription-level usage windows
    /// (5-hour session, weekly caps) with progress bars and live reset countdowns.
    /// Providers without a usage source render a muted "unavailable" card so the layout
    /// is ready for future integrations (Cursor, Codex, Kimi, ...).
    /// </summary>
    public sealed class UsageView : VisualElement
    {
        private const double WarnThreshold = 70;
        private const double DangerThreshold = 90;

        private readonly ScrollView _scroll;
        private readonly Label _updatedLabel;
        private readonly List<(Label Label, DateTimeOffset ResetsAt, string LocalTime)> _countdowns = new();

        public UsageView()
        {
            name = "usage-panel";
            AddToClassList("usage-panel");

            var header = new VisualElement();
            header.AddToClassList("usage-header-row");

            var title = new Label("SUBSCRIPTION USAGE");
            title.AddToClassList("matrix-settings-title");
            title.style.marginBottom = 0;
            header.Add(title);

            _updatedLabel = new Label(string.Empty);
            _updatedLabel.AddToClassList("usage-updated-label");
            header.Add(_updatedLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var refreshButton = new Button(() => UsageService.Refresh(force: true)) { text = "REFRESH" };
            refreshButton.AddToClassList("btn-secondary");
            refreshButton.AddToClassList("btn-compact");
            refreshButton.tooltip = "Re-fetches subscription usage from every provider.";
            header.Add(refreshButton);

            Add(header);

            _scroll = new ScrollView();
            _scroll.style.flexGrow = 1;
            Add(_scroll);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UsageService.Changed += Rebuild;
                UsageService.EnsureLoaded();
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UsageService.Changed -= Rebuild);

            schedule.Execute(TickCountdowns).Every(1000);
            schedule.Execute(() => UsageService.Refresh(force: false)).Every(60_000);
        }

        /// <summary>Called by the router when the view becomes visible.</summary>
        public void OnShown() => UsageService.Refresh(force: false);

        // ── Cards ────────────────────────────────────────────────────────

        private void Rebuild()
        {
            _countdowns.Clear();
            _scroll.Clear();

            DateTime? newestFetch = null;
            foreach (var preset in AgentBackendCatalog.Presets)
            {
                if (preset.Id == "custom")
                    continue;

                var snapshot = UsageService.Get(preset.Id);
                _scroll.Add(BuildCard(preset, snapshot));
                if (snapshot != null && (newestFetch == null || snapshot.FetchedAtUtc > newestFetch))
                    newestFetch = snapshot.FetchedAtUtc;
            }

            _updatedLabel.text = newestFetch != null
                ? $"updated {newestFetch.Value.ToLocalTime():HH:mm:ss}"
                : string.Empty;
        }

        private VisualElement BuildCard(AgentBackendPreset preset, ProviderUsageSnapshot? snapshot)
        {
            var card = new VisualElement();
            card.AddToClassList("agent-config-section");
            card.AddToClassList("usage-card");

            var header = new VisualElement();
            header.AddToClassList("usage-card-header");

            var title = new Label(preset.Label.ToUpperInvariant());
            title.AddToClassList("agent-config-title");
            header.Add(title);

            if (!string.IsNullOrEmpty(snapshot?.PlanName))
            {
                var plan = new Label($"{snapshot!.PlanName!.ToUpperInvariant()} PLAN");
                plan.AddToClassList("usage-plan-label");
                header.Add(plan);
            }

            card.Add(header);

            if (!UsageService.SupportsUsage(preset.Id))
            {
                card.AddToClassList("usage-card--muted");
                var unavailable = new Label("USAGE DATA UNAVAILABLE — this provider does not expose subscription usage yet.");
                unavailable.AddToClassList("usage-note-label");
                card.Add(unavailable);
                return card;
            }

            if (snapshot == null)
            {
                var loading = new Label("FETCHING USAGE DATA…");
                loading.AddToClassList("usage-note-label");
                card.Add(loading);
                return card;
            }

            switch (snapshot.Status)
            {
                case UsageAvailability.AuthRequired:
                    var auth = new Label("⚠ AUTH REQUIRED — " + (snapshot.Error ?? "sign in to this provider."));
                    auth.AddToClassList("usage-auth-label");
                    card.Add(auth);
                    break;

                case UsageAvailability.Error:
                    var error = new Label("✕ " + (snapshot.Error ?? "Failed to fetch usage."));
                    error.AddToClassList("usage-error-label");
                    card.Add(error);
                    break;

                default:
                    if (snapshot.Windows.Count == 0)
                    {
                        var empty = new Label(snapshot.Error ?? "No usage windows reported for this plan.");
                        empty.AddToClassList("usage-note-label");
                        card.Add(empty);
                    }

                    foreach (var window in snapshot.Windows)
                        card.Add(BuildWindowRow(window));
                    break;
            }

            return card;
        }

        private VisualElement BuildWindowRow(UsageWindowSnapshot window)
        {
            var row = new VisualElement();
            row.AddToClassList("usage-window-row");

            var head = new VisualElement();
            head.AddToClassList("usage-window-head");

            var label = new Label(window.Label);
            label.AddToClassList("usage-window-label");
            head.Add(label);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            head.Add(spacer);

            var used = Math.Round(window.UtilizationPercent);
            var values = new Label($"{used:0}% USED · {100 - used:0}% LEFT");
            values.AddToClassList("usage-window-values");
            ApplyThresholdClass(values, window.UtilizationPercent, "usage-window-values");
            head.Add(values);

            row.Add(head);

            var bar = new VisualElement();
            bar.AddToClassList("usage-bar");
            var fill = new VisualElement();
            fill.AddToClassList("usage-bar-fill");
            ApplyThresholdClass(fill, window.UtilizationPercent, "usage-bar-fill");
            fill.style.width = Length.Percent((float)window.UtilizationPercent);
            bar.Add(fill);
            row.Add(bar);

            if (window.ResetsAt != null)
            {
                var reset = new Label(string.Empty);
                reset.AddToClassList("usage-reset-label");
                var entry = (reset, window.ResetsAt.Value, window.ResetsAt.Value.ToLocalTime().ToString("ddd HH:mm"));
                _countdowns.Add(entry);
                UpdateCountdown(entry);
                row.Add(reset);
            }

            return row;
        }

        private static void ApplyThresholdClass(VisualElement element, double utilization, string baseClass)
        {
            if (utilization >= DangerThreshold)
                element.AddToClassList(baseClass + "--danger");
            else if (utilization >= WarnThreshold)
                element.AddToClassList(baseClass + "--warn");
        }

        // ── Reset countdowns ─────────────────────────────────────────────

        private void TickCountdowns()
        {
            foreach (var entry in _countdowns)
                UpdateCountdown(entry);
        }

        private static void UpdateCountdown((Label Label, DateTimeOffset ResetsAt, string LocalTime) entry)
        {
            var remaining = entry.ResetsAt - DateTimeOffset.UtcNow;
            entry.Label.text = remaining <= TimeSpan.Zero
                ? "RESETTING…"
                : $"RESETS IN {FormatRemaining(remaining)} · {entry.LocalTime}";
        }

        internal static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining.TotalDays >= 1)
                return $"{(int)remaining.TotalDays}D {remaining.Hours}H";
            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}H {remaining.Minutes:00}M";
            return $"{remaining.Minutes}M {remaining.Seconds:00}S";
        }
    }
}
