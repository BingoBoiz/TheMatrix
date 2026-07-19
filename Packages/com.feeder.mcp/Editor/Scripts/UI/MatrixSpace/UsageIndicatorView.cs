#nullable enable

using System;
using System.Text;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Compact topbar chip: highest subscription-window utilization across all providers
    /// at a glance (◔ 34%), amber/red past the warn/danger thresholds, full per-provider
    /// breakdown in the tooltip. Clicking opens the USAGE view.
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
                    lines.Append($"\n{preset.Label.ToUpperInvariant()}: sign-in required");
                    continue;
                }

                if (snapshot.Status != UsageAvailability.Ok || snapshot.Windows.Count == 0)
                {
                    lines.Append($"\n{preset.Label.ToUpperInvariant()}: unavailable");
                    continue;
                }

                foreach (var window in snapshot.Windows)
                {
                    max = Math.Max(max, window.UtilizationPercent);
                    lines.Append($"\n{preset.Label.ToUpperInvariant()} · {window.Label}: {Math.Round(window.UtilizationPercent):0}% used");
                    if (window.ResetsAt != null)
                        lines.Append($" · resets {window.ResetsAt.Value.ToLocalTime():ddd HH:mm}");
                }
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
                _label.text = "◔ !";
                EnableInClassList("usage-chip--warn", authHint != null);
                tooltip = lines.ToString();
                return;
            }

            _label.text = $"◔ {Math.Round(max):0}%";
            EnableInClassList("usage-chip--danger", max >= DangerThreshold);
            EnableInClassList("usage-chip--warn", max is >= WarnThreshold and < DangerThreshold);
            tooltip = lines.ToString();
        }
    }
}
