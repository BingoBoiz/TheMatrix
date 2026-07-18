#nullable enable

using System;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// One pane of the Matrix Space grid: header (LED, name, state, cost), transcript,
    /// and prompt input, bound to a single <see cref="AgentSession"/>.
    /// </summary>
    public sealed class AgentPaneView : VisualElement
    {
        private readonly Label _nameLabel;
        private readonly Label _stateLabel;
        private readonly Label _costLabel;
        private readonly VisualElement _statusLed;
        private readonly ScrollView _transcriptScroll;
        private readonly VisualElement _transcriptContent;
        private readonly TextField _promptInput;
        private readonly Button _sendButton;
        private readonly Button _stopButton;

        private readonly DropdownField _backendDropdown;
        private readonly DropdownField _modelDropdown;
        private readonly VisualElement _configRow;

        private AgentSession? _session;
        private int _renderedEntryCount;
        private string _backendLabel = string.Empty;

        /// <summary>Raised when the user clicks the reset button; the window owns pane lifecycle.</summary>
        public event Action<AgentPaneView>? ResetRequested;

        /// <summary>Raised when the user picks another agent backend (id from the catalog).</summary>
        public event Action<AgentPaneView, string>? BackendChangeRequested;

        /// <summary>Raised when the user picks a model ("default" = CLI default).</summary>
        public event Action<AgentPaneView, string>? ModelChangeRequested;

        /// <summary>Raised when the user toggles the expand button; the grid owns the layout.</summary>
        public event Action<AgentPaneView>? ExpandToggleRequested;

        /// <summary>Raised when the pane gains keyboard focus anywhere inside it.</summary>
        public event Action<AgentPaneView>? Focused;

        public AgentSession? Session => _session;

        public AgentPaneView(VisualTreeAsset paneTemplate)
        {
            paneTemplate.CloneTree(this);
            style.flexGrow = 1;
            style.flexBasis = 0;

            _nameLabel = this.Q<Label>("agent-name");
            _stateLabel = this.Q<Label>("agent-state");
            _costLabel = this.Q<Label>("agent-cost");
            _statusLed = this.Q<VisualElement>("status-led");
            _transcriptScroll = this.Q<ScrollView>("transcript-scroll");
            _transcriptContent = this.Q<VisualElement>("transcript-content");
            _promptInput = this.Q<TextField>("prompt-input");
            _sendButton = this.Q<Button>("send-button");
            _stopButton = this.Q<Button>("stop-button");
            _configRow = this.Q<VisualElement>("pane-config-row");

            var backendChoices = new System.Collections.Generic.List<string>();
            foreach (var preset in AgentBackendCatalog.Presets)
                backendChoices.Add(preset.Label);
            _backendDropdown = new DropdownField(backendChoices, 0)
            {
                tooltip = "Agent backend for this pane. Switching starts a fresh session.",
            };
            _backendDropdown.AddToClassList("agent-pane-backend");
            _backendDropdown.AddToClassList("styled-dropdown");
            _backendDropdown.RegisterValueChangedCallback(evt =>
            {
                var index = backendChoices.IndexOf(evt.newValue);
                if (index >= 0)
                    BackendChangeRequested?.Invoke(this, AgentBackendCatalog.Presets[index].Id);
            });
            this.Q<VisualElement>("backend-slot").Add(_backendDropdown);

            _modelDropdown = new DropdownField(new System.Collections.Generic.List<string> { "default" }, 0)
            {
                tooltip = "Model for this pane. Edit the model list per agent in CONFIG > AGENTS.",
            };
            _modelDropdown.AddToClassList("agent-pane-model");
            _modelDropdown.AddToClassList("styled-dropdown");
            _modelDropdown.RegisterValueChangedCallback(evt => ModelChangeRequested?.Invoke(this, evt.newValue));
            this.Q<VisualElement>("backend-slot").Add(_modelDropdown);

            _sendButton.clicked += SendCurrentPrompt;
            _stopButton.clicked += () => _session?.Cancel();
            this.Q<Button>("reset-button").clicked += () => ResetRequested?.Invoke(this);
            this.Q<Button>("close-button").clicked += () => ResetRequested?.Invoke(this);
            this.Q<Button>("config-button").clicked += ToggleConfigRow;
            this.Q<Button>("expand-button").clicked += () => ExpandToggleRequested?.Invoke(this);

            RegisterCallback<FocusInEvent>(_ => Focused?.Invoke(this), TrickleDown.TrickleDown);

            // Enter sends, Shift+Enter inserts a newline. TrickleDown so the TextField
            // does not swallow the event first.
            _promptInput.RegisterCallback<KeyDownEvent>(OnPromptKeyDown, TrickleDown.TrickleDown);
        }

        private void ToggleConfigRow()
        {
            var visible = _configRow.resolvedStyle.display != DisplayStyle.None;
            _configRow.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetActive(bool active)
            => this.Q<VisualElement>("agent-pane")?.EnableInClassList("agent-pane--active", active);

        public void SetBackendSelection(string backendId)
        {
            _backendLabel = AgentBackendCatalog.Get(backendId).Label;
            _backendDropdown.SetValueWithoutNotify(_backendLabel);
            RefreshModelChoices(backendId);
            if (_session != null)
                _nameLabel.text = BuildTitle(_session);
        }

        public void SetModelSelection(string? model)
        {
            var value = string.IsNullOrEmpty(model) ? "default" : model!;
            if (!_modelDropdown.choices.Contains(value))
                _modelDropdown.choices.Add(value);
            _modelDropdown.SetValueWithoutNotify(value);
        }

        public void RefreshModelChoices(string backendId)
        {
            var preset = AgentBackendCatalog.Get(backendId);
            var models = AgentBackendCatalog.GetModels(preset);
            _modelDropdown.choices = models;
            _modelDropdown.style.display = models.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!models.Contains(_modelDropdown.value))
                _modelDropdown.SetValueWithoutNotify(models[0]);
        }

        public void Bind(AgentSession session)
        {
            Unbind();
            _session = session;
            session.Changed += OnSessionChanged;

            _transcriptContent.Clear();
            _renderedEntryCount = 0;
            OnSessionChanged(session);
        }

        public void Unbind()
        {
            if (_session != null)
                _session.Changed -= OnSessionChanged;
            _session = null;
        }

        private void OnPromptKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.Return && evt.keyCode != UnityEngine.KeyCode.KeypadEnter)
                return;
            if (evt.shiftKey)
                return;

            evt.StopImmediatePropagation();
#if !UNITY_2023_2_OR_NEWER
            evt.PreventDefault();
#endif
            SendCurrentPrompt();
        }

        private void SendCurrentPrompt()
        {
            if (_session is not { } session || !session.CanSend)
                return;

            var prompt = _promptInput.value;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            _promptInput.SetValueWithoutNotify(string.Empty);
            session.Send(prompt);
            _promptInput.Focus();
        }

        private string BuildTitle(AgentSession session)
        {
            var label = string.IsNullOrEmpty(_backendLabel)
                ? AgentBackendCatalog.Get(session.BackendId).Label
                : _backendLabel;
            return $"{label} · {session.DisplayName.ToLowerInvariant()}";
        }

        private void OnSessionChanged(AgentSession session)
        {
            _nameLabel.text = BuildTitle(session);
            _stateLabel.text = GetStateText(session.State);
            _costLabel.text = session.TotalCostUsd > 0 ? $"${session.TotalCostUsd:F4}" : string.Empty;

            UpdateLed(session.State);

            var busy = session.State is AgentSessionState.Starting or AgentSessionState.Streaming;
            _sendButton.style.display = busy ? DisplayStyle.None : DisplayStyle.Flex;
            _stopButton.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _promptInput.SetEnabled(session.State != AgentSessionState.Exited);
            _backendDropdown.SetEnabled(!busy);
            _modelDropdown.SetEnabled(!busy);

            SyncTranscript(session);
        }

        private void SyncTranscript(AgentSession session)
        {
            var wasAtBottom = IsScrolledToBottom();

            // Append missing entries…
            for (var i = _renderedEntryCount; i < session.Transcript.Count; i++)
                _transcriptContent.Add(new TranscriptEntryView(session.Transcript[i]));
            _renderedEntryCount = session.Transcript.Count;

            // …and refresh the last one (streaming assistant text mutates in place).
            if (_transcriptContent.childCount > 0 &&
                _transcriptContent[_transcriptContent.childCount - 1] is TranscriptEntryView lastView)
            {
                lastView.Refresh();
            }

            if (wasAtBottom)
                _transcriptScroll.schedule.Execute(ScrollToBottom);
        }

        private bool IsScrolledToBottom()
        {
            var scroller = _transcriptScroll.verticalScroller;
            return scroller.highValue <= 0 || scroller.value >= scroller.highValue - 20;
        }

        private void ScrollToBottom()
        {
            var scroller = _transcriptScroll.verticalScroller;
            scroller.value = scroller.highValue;
        }

        private void UpdateLed(AgentSessionState state)
        {
            _statusLed.EnableInClassList("status-indicator-circle-online", state == AgentSessionState.WaitingInput);
            _statusLed.EnableInClassList("status-indicator-circle-connecting",
                state is AgentSessionState.Starting or AgentSessionState.Streaming);
            _statusLed.EnableInClassList("status-indicator-circle-disconnected",
                state is AgentSessionState.Idle or AgentSessionState.Exited);
            _statusLed.EnableInClassList("led-error", state == AgentSessionState.Error);
        }

        private static string GetStateText(AgentSessionState state) => state switch
        {
            AgentSessionState.Idle => "DORMANT",
            AgentSessionState.Starting => "ESTABLISHING LINK",
            AgentSessionState.Streaming => "COMPILING RESPONSE",
            AgentSessionState.WaitingInput => "AWAITING DIRECTIVE",
            AgentSessionState.Error => "ANOMALY",
            _ => "OFFLINE",
        };
    }
}
