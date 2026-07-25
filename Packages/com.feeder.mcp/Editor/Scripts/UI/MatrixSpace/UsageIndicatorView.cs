#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Compact topbar chip: one utilization value per authenticated AI CLI provider
    /// (for example, "CLAUDE 66% · CODEX 14%"). Each provider uses its highest active
    /// subscription window; the tooltip keeps the full window breakdown. Clicking opens
    /// the USAGE view.
    /// </summary>
    public sealed class UsageIndicatorView : VisualElement
    {
        private const double WarnThreshold = 70;
        private const double DangerThreshold = 90;

        public event Action? Clicked;

        private readonly Label _label;

        public UsageIndicatorView()
        {
            name = "usage-chip";
            AddToClassList("usage-chip");
            tooltip = "Subscription usage. Click for details.";

            _label = new Label("◔ ‥");
            _label.AddToClassList("usage-chip-label");
            Add(_label);

            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UsageService.Changed += Refresh;
                UsageService.EnsureLoaded();
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UsageService.Changed -= Refresh);

            schedule.Execute(() => UsageService.Refresh(force: false)).Every(60_000);
        }

        private void Refresh()
        {
            EnableInClassList("usage-chip--warn", false);
            EnableInClassList("usage-chip--danger", false);

            double max = -1;
            var anySnapshot = false;
            var authHint = (string?)null;
            var summaries = new List<string>();
            var lines = new StringBuilder("Subscription usage:");

            foreach (var preset in AgentBackendCatalog.Presets)
            {
                if (!UsageService.SupportsUsage(preset.Id))
                    continue;

                var snapshot = UsageService.Get(preset.Id);
                if (snapshot == null)
                    continue;

                anySnapshot = true;
                if (snapshot.Status == UsageAvailability.AuthRequired)
                {
                    authHint = snapshot.Error ?? $"{preset.Label} sign-in required.";
                    summaries.Add($"{ShortLabel(preset)} !");
                    lines.Append($"\n{preset.Label.ToUpperInvariant()}: sign-in required");
                    continue;
                }

                if (snapshot.Status != UsageAvailability.Ok || snapshot.Windows.Count == 0)
                {
                    summaries.Add($"{ShortLabel(preset)} ?");
                    lines.Append($"\n{preset.Label.ToUpperInvariant()}: unavailable");
                    continue;
                }

                double providerMax = -1;
                foreach (var window in snapshot.Windows)
                {
                    providerMax = Math.Max(providerMax, window.UtilizationPercent);
                    max = Math.Max(max, window.UtilizationPercent);
                    lines.Append($"\n{preset.Label.ToUpperInvariant()} · {window.Label}: {Math.Round(window.UtilizationPercent):0}% used");
                    if (window.ResetsAt != null)
                        lines.Append($" · resets {window.ResetsAt.Value.ToLocalTime():ddd HH:mm}");
                }

                summaries.Add($"{ShortLabel(preset)} {Math.Round(providerMax):0}%");
            }

            if (!anySnapshot)
            {
                _label.text = "◔ ‥";
                tooltip = "Subscription usage: fetching… Click for details.";
                return;
            }

            if (max < 0)
            {
                // Snapshots exist but none delivered windows (auth needed / errors).
                _label.text = summaries.Count > 0
                    ? "◔ " + string.Join(" · ", summaries)
                    : "◔ !";
                EnableInClassList("usage-chip--warn", authHint != null);
                tooltip = lines.ToString();
                return;
            }

            _label.text = "◔ " + string.Join(" · ", summaries);
            EnableInClassList("usage-chip--danger", max >= DangerThreshold);
            EnableInClassList("usage-chip--warn", max is >= WarnThreshold and < DangerThreshold);
            tooltip = lines.ToString();
        }

        private static string ShortLabel(AgentBackendPreset preset)
        {
            var label = preset.Label;
            var qualifier = label.IndexOf(" (", StringComparison.Ordinal);
            if (qualifier > 0)
                label = label.Substring(0, qualifier);
            if (label.EndsWith(" CLI", StringComparison.OrdinalIgnoreCase))
                label = label.Substring(0, label.Length - 4);
            if (label.EndsWith(" Code", StringComparison.OrdinalIgnoreCase))
                label = label.Substring(0, label.Length - 5);
            return label.ToUpperInvariant();
        }
    }
}
