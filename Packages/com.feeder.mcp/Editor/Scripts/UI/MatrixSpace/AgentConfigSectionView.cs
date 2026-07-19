#nullable enable

using System.Collections.Generic;
using Feeder.MCP.Editor.MatrixSpace;
using Feeder.MCP.Editor.MatrixSpace.Setup;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// One backend's block inside CONFIG > AGENTS: status line (installed/auth), LOGIN /
    /// INSTALL / TEST buttons, executable + arguments + models + environment fields.
    /// </summary>
    public sealed class AgentConfigSectionView : VisualElement
    {
        private readonly AgentBackendPreset _preset;
        private readonly VisualElement _statusDot;
        private readonly Label _statusLabel;
        private readonly Label? _modelsStatusLabel;
        private readonly Foldout? _setupLogFoldout;
        private readonly Label? _setupLogLabel;
        private readonly ScrollView? _setupLogScroll;
        private readonly List<Button> _actionButtons = new();

        public AgentConfigSectionView(AgentBackendPreset preset)
        {
            _preset = preset;
            AddToClassList("agent-config-section");

            // ── Header: name + status + actions ──
            var header = new VisualElement();
            header.AddToClassList("agent-config-header");

            _statusDot = new VisualElement();
            _statusDot.AddToClassList("status-indicator-circle");
            _statusDot.AddToClassList("agent-config-status-dot");
            header.Add(_statusDot);

            var title = new Label(preset.Label.ToUpperInvariant());
            title.AddToClassList("agent-config-title");
            if (preset.Note != null)
                title.tooltip = preset.Note;
            header.Add(title);

            _statusLabel = new Label("…");
            _statusLabel.AddToClassList("agent-config-status");
            header.Add(_statusLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            if (AgentSetupCatalog.Get(preset.Id) != null)
            {
                var setupButton = new Button(() => AgentSetupService.RunSetup(_preset)) { text = "SETUP" };
                setupButton.AddToClassList("btn-primary");
                setupButton.AddToClassList("btn-compact");
                setupButton.tooltip = "Installs the CLI and writes the MCP config automatically. " +
                                      "Only account sign-in (LOGIN) stays manual.";
                header.Add(setupButton);
                _actionButtons.Add(setupButton);
            }

            var loginButton = new Button(OpenLoginTerminal) { text = "LOGIN" };
            loginButton.AddToClassList("btn-secondary");
            loginButton.AddToClassList("btn-compact");
            loginButton.tooltip = string.IsNullOrEmpty(preset.LoginHint)
                ? "Opens a terminal running this CLI so you can sign in."
                : preset.LoginHint;
            header.Add(loginButton);
            _actionButtons.Add(loginButton);

            if (!string.IsNullOrEmpty(preset.InstallUrl))
            {
                var installButton = new Button(() => UnityEngine.Application.OpenURL(_preset.InstallUrl)) { text = "INSTALL" };
                installButton.AddToClassList("btn-secondary");
                installButton.AddToClassList("btn-compact");
                installButton.tooltip = "Opens the install guide: " + preset.InstallUrl;
                header.Add(installButton);
                _actionButtons.Add(installButton);
            }

            var testButton = new Button(() => AgentStatusService.Probe(_preset)) { text = "TEST" };
            testButton.AddToClassList("btn-tertiary");
            testButton.tooltip = "Re-checks whether the CLI is installed and responding.";
            header.Add(testButton);
            _actionButtons.Add(testButton);

            Add(header);

            if (AgentSetupCatalog.Get(preset.Id) != null)
            {
                _setupLogFoldout = new Foldout { text = "SETUP LOG", value = false };
                _setupLogFoldout.AddToClassList("agent-setup-log-foldout");
                _setupLogFoldout.style.display = DisplayStyle.None;

                _setupLogScroll = new ScrollView(ScrollViewMode.Vertical);
                _setupLogScroll.AddToClassList("agent-setup-log");

                _setupLogLabel = new Label();
                _setupLogLabel.AddToClassList("agent-setup-log-text");
                _setupLogLabel.selection.isSelectable = true;
                _setupLogScroll.Add(_setupLogLabel);

                _setupLogFoldout.Add(_setupLogScroll);
                Add(_setupLogFoldout);
            }

            // ── Fields ──
            if (_preset.IsClaude)
            {
                var exeField = MakeField("Executable", MatrixSpaceSettings.ClaudeExecutablePath.Value,
                    "Explicit path to claude executable. Empty = auto-discover.");
                exeField.RegisterValueChangedCallback(evt =>
                {
                    MatrixSpaceSettings.ClaudeExecutablePath.Value = evt.newValue;
                    ClaudeCliLocator.InvalidateCache();
                });
                Add(exeField);
            }
            else
            {
                var exeSetting = AgentBackendCatalog.ExecutableSetting(_preset);
                var exeField = MakeField("Executable", exeSetting.Value,
                    "Executable name (resolved via PATH) or full path.");
                exeField.RegisterValueChangedCallback(evt => { var s = exeSetting; s.Value = evt.newValue; });
                Add(exeField);

                var argsSetting = AgentBackendCatalog.ArgumentsSetting(_preset);
                var argsField = MakeField("Arguments", argsSetting.Value,
                    "{prompt} = quoted prompt (omit to pipe via stdin); {model} = model argument.");
                argsField.RegisterValueChangedCallback(evt => { var s = argsSetting; s.Value = evt.newValue; });
                Add(argsField);
            }

            var modelsSetting = AgentBackendCatalog.ModelsSetting(_preset);
            var modelsField = MakeField("Models", modelsSetting.Value,
                "Manual override: comma-separated list shown in the pane model dropdown. " +
                "Leave empty to auto-fetch the latest models. \"default\" = no model flag.");
            modelsField.RegisterValueChangedCallback(evt =>
            {
                var s = modelsSetting;
                s.Value = evt.newValue;
                RefreshModelsStatus();
                ModelsEdited?.Invoke();
            });
            Add(modelsField);

            var hasDynamicModels = _preset.ModelsDevProviderId != null ||
                                   !string.IsNullOrEmpty(_preset.ListModelsArgs);
            if (hasDynamicModels)
            {
                var modelsRow = new VisualElement();
                modelsRow.style.flexDirection = FlexDirection.Row;
                modelsRow.style.alignItems = Align.Center;

                var refreshButton = new Button(ModelCatalogService.ForceRefresh) { text = "REFRESH MODELS" };
                refreshButton.AddToClassList("btn-tertiary");
                refreshButton.tooltip = "Re-fetches the live model catalog (models.dev / the CLI itself).";
                modelsRow.Add(refreshButton);

                _modelsStatusLabel = new Label();
                _modelsStatusLabel.AddToClassList("agent-config-status");
                modelsRow.Add(_modelsStatusLabel);

                Add(modelsRow);
            }

            var envSetting = AgentBackendCatalog.EnvironmentSetting(_preset);
            var envField = MakeField("Env vars", envSetting.Value,
                "One KEY=VALUE per line, passed to the agent process (e.g. ANTHROPIC_API_KEY=..., OPENAI_API_KEY=...). " +
                "Stored as plain text in PlayerPrefs on this machine.");
            envField.multiline = true;
            envField.AddToClassList("agent-config-env");
            envField.RegisterValueChangedCallback(evt => { var s = envSetting; s.Value = evt.newValue; });
            Add(envField);

            AgentStatusService.Changed += RefreshStatus;
            ModelCatalogService.Updated += RefreshModelsStatus;
            AgentSetupService.Changed += RefreshSetup;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                AgentStatusService.Changed -= RefreshStatus;
                ModelCatalogService.Updated -= RefreshModelsStatus;
                AgentSetupService.Changed -= RefreshSetup;
            });
            RefreshStatus();
            RefreshModelsStatus();
            RefreshSetup();
        }

        /// <summary>Raised when the model list text changes (panes refresh their dropdowns).</summary>
        public event System.Action? ModelsEdited;

        private static TextField MakeField(string label, string value, string tooltip)
        {
            var field = new TextField(label) { tooltip = tooltip };
            field.AddToClassList("styled-text-field");
            field.AddToClassList("agent-config-field");
            field.SetValueWithoutNotify(value);
            return field;
        }

        private void RefreshSetup()
        {
            var state = AgentSetupService.GetState(_preset);
            var isActive = state.Phase is AgentSetupPhase.Running or AgentSetupPhase.Queued;

            foreach (var button in _actionButtons)
                button.SetEnabled(!AgentSetupService.IsAnyRunning);

            if (_setupLogFoldout == null || _setupLogLabel == null)
                return;

            _setupLogFoldout.style.display = state.Log.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (isActive)
                _setupLogFoldout.value = true;

            _setupLogLabel.text = string.Join("\n", state.Log);
            if (_setupLogScroll is { } scroll)
                scroll.schedule.Execute(() => scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue));

            if (isActive)
            {
                _statusLabel.text = state.CurrentStep.Length > 0
                    ? $"SETUP: {state.CurrentStep}…"
                    : "SETUP…";
            }
            else if (state.Phase != AgentSetupPhase.Idle)
            {
                // Setup just finished: re-render the availability line (Probe refreshes it again).
                RefreshStatus();
            }
        }

        private void RefreshStatus()
        {
            // While setup runs, the status line shows the current setup step instead.
            var setupPhase = AgentSetupService.GetState(_preset).Phase;
            if (setupPhase is AgentSetupPhase.Running or AgentSetupPhase.Queued)
                return;

            var info = AgentStatusService.GetStatus(_preset);
            var (text, cssClass) = info.Availability switch
            {
                AgentAvailability.Ready => ($"READY {info.Version}", "status-indicator-circle-online"),
                AgentAvailability.AuthRequired => ("AUTH REQUIRED — use LOGIN", "led-error"),
                AgentAvailability.NotInstalled => ("NOT INSTALLED — use INSTALL", "led-error"),
                AgentAvailability.Probing => ("CHECKING…", "status-indicator-circle-connecting"),
                _ => ("UNRESPONSIVE — check executable", "led-error"),
            };

            _statusLabel.text = text;
            _statusDot.ClearClassList();
            _statusDot.AddToClassList("status-indicator-circle");
            _statusDot.AddToClassList("agent-config-status-dot");
            _statusDot.AddToClassList(cssClass);

            if (info.ResolvedPath != null)
                _statusLabel.tooltip = info.ResolvedPath;
        }

        private void RefreshModelsStatus()
        {
            if (_modelsStatusLabel == null)
                return;

            var overrideCsv = AgentBackendCatalog.ModelsSetting(_preset).Value;
            var hasOverride = !string.IsNullOrWhiteSpace(overrideCsv) &&
                              overrideCsv.Trim() != _preset.DefaultModels;

            string text;
            if (hasOverride)
                text = "MODELS: manual override";
            else if (ModelCatalogService.IsRefreshing)
                text = "MODELS: refreshing…";
            else if (ModelCatalogService.LastUpdatedUtc is { } updated)
                text = $"MODELS: auto (updated {FormatAge(updated)} ago)";
            else if (ModelCatalogService.LastError != null)
                text = "MODELS: offline — using built-in list";
            else
                text = "MODELS: auto";

            _modelsStatusLabel.text = text;
            _modelsStatusLabel.tooltip = ModelCatalogService.LastError ?? string.Empty;
        }

        private static string FormatAge(System.DateTime utc)
        {
            var age = System.DateTime.UtcNow - utc;
            if (age.TotalMinutes < 1)
                return "moments";
            if (age.TotalHours < 1)
                return $"{(int)age.TotalMinutes}m";
            if (age.TotalDays < 1)
                return $"{(int)age.TotalHours}h";
            return $"{(int)age.TotalDays}d";
        }

        /// <summary>
        /// Opens the vendor CLI's own login flow in a terminal (via <see cref="AgentLoginLauncher"/>).
        /// When the CLI isn't installed yet, falls back to the install page.
        /// </summary>
        private void OpenLoginTerminal()
        {
            if (!AgentLoginLauncher.Open(_preset) && !string.IsNullOrEmpty(_preset.InstallUrl))
                UnityEngine.Application.OpenURL(_preset.InstallUrl);
        }
    }
}
