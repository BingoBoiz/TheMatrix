#nullable enable

using System.Diagnostics;
using Feeder.MCP.Editor.MatrixSpace;
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

            var loginButton = new Button(OpenLoginTerminal) { text = "LOGIN" };
            loginButton.AddToClassList("btn-secondary");
            loginButton.AddToClassList("btn-compact");
            loginButton.tooltip = string.IsNullOrEmpty(preset.LoginHint)
                ? "Opens a terminal running this CLI so you can sign in."
                : preset.LoginHint;
            header.Add(loginButton);

            if (!string.IsNullOrEmpty(preset.InstallUrl))
            {
                var installButton = new Button(() => UnityEngine.Application.OpenURL(_preset.InstallUrl)) { text = "INSTALL" };
                installButton.AddToClassList("btn-secondary");
                installButton.AddToClassList("btn-compact");
                installButton.tooltip = "Opens the install guide: " + preset.InstallUrl;
                header.Add(installButton);
            }

            var testButton = new Button(() => AgentStatusService.Probe(_preset)) { text = "TEST" };
            testButton.AddToClassList("btn-tertiary");
            testButton.tooltip = "Re-checks whether the CLI is installed and responding.";
            header.Add(testButton);

            Add(header);

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
                "Comma-separated list shown in the pane model dropdown. Add new models freely (e.g. gpt-5.6). \"default\" = no model flag.");
            modelsField.RegisterValueChangedCallback(evt =>
            {
                var s = modelsSetting;
                s.Value = evt.newValue;
                ModelsEdited?.Invoke();
            });
            Add(modelsField);

            var envSetting = AgentBackendCatalog.EnvironmentSetting(_preset);
            var envField = MakeField("Env vars", envSetting.Value,
                "One KEY=VALUE per line, passed to the agent process (e.g. ANTHROPIC_API_KEY=..., OPENAI_API_KEY=...). " +
                "Stored as plain text in PlayerPrefs on this machine.");
            envField.multiline = true;
            envField.AddToClassList("agent-config-env");
            envField.RegisterValueChangedCallback(evt => { var s = envSetting; s.Value = evt.newValue; });
            Add(envField);

            AgentStatusService.Changed += RefreshStatus;
            RegisterCallback<DetachFromPanelEvent>(_ => AgentStatusService.Changed -= RefreshStatus);
            RefreshStatus();
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

        private void RefreshStatus()
        {
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

        /// <summary>
        /// Opens a real terminal window running the CLI's login flow. Sign-in happens in the
        /// user's own browser/terminal — Matrix Space never touches credentials.
        /// </summary>
        private void OpenLoginTerminal()
        {
            var path = AgentBackendCatalog.ResolveExecutablePath(_preset);
            if (path == null)
            {
                if (!string.IsNullOrEmpty(_preset.InstallUrl))
                    UnityEngine.Application.OpenURL(_preset.InstallUrl);
                return;
            }

            var command = $"\"{path}\" {_preset.LoginArgs}".TrimEnd();
            var hint = string.IsNullOrEmpty(_preset.LoginHint) ? string.Empty : $"echo {_preset.LoginHint} & ";
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{hint}{command}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = MatrixSpacePaths.ProjectRoot,
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[MatrixSpace] Failed to open login terminal: {ex.Message}");
            }
        }
    }
}
