#nullable enable

using System;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine;
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
        private readonly DropdownField _modeDropdown;
        private readonly DropdownField _effortDropdown;
        private readonly VisualElement _configRow;
        private readonly Label _configUsageLabel;

        private const float ConfigBackendDesignWidth = 118f;
        private const float ConfigModelDesignWidth = 92f;
        private const float ConfigModeDesignWidth = 72f;
        private const float ConfigEffortDesignWidth = 72f;
        private const float ConfigUsageMinWidth = 90f;
        private const float ConfigControlGap = 4f;

        private static readonly System.Collections.Generic.List<string> ModeChoices = new() { "MANUAL", "PLAN", "AUTO" };
        private static readonly System.Collections.Generic.List<string> EffortChoices =
            new(MatrixSpaceSettings.EffortLevels);

        private readonly System.Collections.Generic.List<string> _backendChoices = new();
        private readonly System.Collections.Generic.List<TranscriptEntryView> _entryViews = new();
        private readonly Label _workingIndicator;
        private IVisualElementScheduledItem? _workingAnimation;
        private int _workingDotPhase;

        private AgentSession? _session;
        private int _renderedEntryCount;
        private string _backendLabel = string.Empty;
        private string _selectedBackendId = string.Empty;

        /// <summary>Raised when the user clicks the reset button; the window owns pane lifecycle.</summary>
        public event Action<AgentPaneView>? ResetRequested;

        /// <summary>Raised when the user picks another agent backend (id from the catalog).</summary>
        public event Action<AgentPaneView, string>? BackendChangeRequested;

        /// <summary>Raised when the user picks a model ("default" = CLI default).</summary>
        public event Action<AgentPaneView, string>? ModelChangeRequested;

        /// <summary>Raised when the user picks an agent mode ("manual"/"plan"/"auto").</summary>
        public event Action<AgentPaneView, string>? ModeChangeRequested;

        /// <summary>Raised when the user picks a reasoning effort ("default" = CLI default).</summary>
        public event Action<AgentPaneView, string>? EffortChangeRequested;

        /// <summary>Raised when the unsent prompt draft changes.</summary>
        public event Action<AgentPaneView, string>? PromptDraftChanged;

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
            _configUsageLabel = this.Q<Label>("config-usage-label");

            _workingIndicator = new Label();
            _workingIndicator.AddToClassList("matrix-working-indicator");
            _workingIndicator.style.display = DisplayStyle.None;
            _transcriptContent.Add(_workingIndicator);

            foreach (var preset in AgentBackendCatalog.Presets)
                _backendChoices.Add(preset.Label);
            _backendDropdown = new DropdownField(_backendChoices, 0)
            {
                tooltip = "Agent backend for this pane. Switching starts a fresh session.",
            };
            _backendDropdown.AddToClassList("agent-pane-backend");
            _backendDropdown.AddToClassList("agent-pane-config-dropdown");
            _backendDropdown.AddToClassList("agent-pane-config-control");
            _backendDropdown.AddToClassList("styled-dropdown");
            _backendDropdown.RegisterValueChangedCallback(evt =>
            {
                var index = _backendChoices.IndexOf(evt.newValue);
                if (index >= 0)
                    BackendChangeRequested?.Invoke(this, AgentBackendCatalog.Presets[index].Id);
            });
            _modelDropdown = new DropdownField(new System.Collections.Generic.List<string> { "default" }, 0)
            {
                tooltip = "Model for this pane. Edit the model list per agent in CONFIG > AGENTS.",
            };
            _modelDropdown.AddToClassList("agent-pane-model");
            _modelDropdown.AddToClassList("agent-pane-config-dropdown");
            _modelDropdown.AddToClassList("agent-pane-config-control");
            _modelDropdown.AddToClassList("styled-dropdown");
            _modelDropdown.RegisterValueChangedCallback(evt =>
            {
                ModelChangeRequested?.Invoke(this, evt.newValue);
                ScheduleConfigRowLayout();
            });
            _modeDropdown = new DropdownField(ModeChoices, IndexOfMode(MatrixSpaceSettings.DefaultAgentMode))
            {
                tooltip = "Agent mode. MANUAL: safe, Unity tools only. PLAN: research and propose only. " +
                          "AUTO: the agent edits files and runs commands directly.",
            };
            _modeDropdown.AddToClassList("agent-pane-mode");
            _modeDropdown.AddToClassList("agent-pane-config-dropdown");
            _modeDropdown.AddToClassList("agent-pane-config-control");
            _modeDropdown.AddToClassList("styled-dropdown");
            _modeDropdown.RegisterValueChangedCallback(evt =>
            {
                var index = ModeChoices.IndexOf(evt.newValue);
                if (index >= 0)
                    ModeChangeRequested?.Invoke(this, MatrixSpaceSettings.AgentModes[index]);
            });
            _effortDropdown = new DropdownField(EffortChoices, 0)
            {
                tooltip = "Reasoning effort. Higher = more thorough, slower and costlier. default = CLI default.",
            };
            _effortDropdown.AddToClassList("agent-pane-effort");
            _effortDropdown.AddToClassList("agent-pane-config-dropdown");
            _effortDropdown.AddToClassList("agent-pane-config-control");
            _effortDropdown.AddToClassList("styled-dropdown");
            _effortDropdown.RegisterValueChangedCallback(evt =>
            {
                EffortChangeRequested?.Invoke(this, evt.newValue);
                ScheduleConfigRowLayout();
            });
            _configRow.Insert(0, _effortDropdown);
            _configRow.Insert(0, _modeDropdown);
            _configRow.Insert(0, _modelDropdown);
            _configRow.Insert(0, _backendDropdown);

            _sendButton.clicked += SendCurrentPrompt;
            _stopButton.clicked += () => _session?.Cancel();
            this.Q<Button>("close-button").clicked += () => ResetRequested?.Invoke(this);
            this.Q<Button>("config-button").clicked += ToggleConfigRow;
            this.Q<Button>("expand-button").clicked += () => ExpandToggleRequested?.Invoke(this);

            RegisterCallback<FocusInEvent>(_ => Focused?.Invoke(this), TrickleDown.TrickleDown);

            // Enter sends, Shift+Enter inserts a newline. TrickleDown so the TextField
            // does not swallow the event first.
            _promptInput.RegisterCallback<KeyDownEvent>(OnPromptKeyDown, TrickleDown.TrickleDown);
            _promptInput.RegisterValueChangedCallback(evt => PromptDraftChanged?.Invoke(this, evt.newValue ?? string.Empty));

            _configRow.RegisterCallback<GeometryChangedEvent>(_ => ScheduleConfigRowLayout());
            RegisterCallback<GeometryChangedEvent>(_ => ScheduleConfigRowLayout());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AgentStatusService.Changed += OnAgentStatusChanged;
                OnAgentStatusChanged();
                ScheduleConfigRowLayout();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => AgentStatusService.Changed -= OnAgentStatusChanged);
        }

        private void OnAgentStatusChanged()
        {
            RebuildBackendChoices();
            ApplySelectedBackendStatusClass();
        }

        /// <summary>Prefix each backend label with an availability glyph so users can tell what runs now.</summary>
        private void RebuildBackendChoices()
        {
            _backendChoices.Clear();
            foreach (var preset in AgentBackendCatalog.Presets)
                _backendChoices.Add(DecorateBackendLabel(preset));
            _backendDropdown.choices = _backendChoices;

            var index = Array.FindIndex(AgentBackendCatalog.Presets, p => p.Id == _selectedBackendId);
            if (index >= 0)
                _backendDropdown.SetValueWithoutNotify(_backendChoices[index]);
        }

        private static string DecorateBackendLabel(AgentBackendPreset preset)
        {
            return AgentStatusService.GetStatus(preset).Availability switch
            {
                AgentAvailability.Ready => $"● {preset.Label}",
                AgentAvailability.NotInstalled => $"⚠ {preset.Label}",
                AgentAvailability.AuthRequired => $"⚠ {preset.Label}",
                _ => $"◌ {preset.Label}",
            };
        }

        private void ApplySelectedBackendStatusClass()
        {
            var preset = AgentBackendCatalog.Get(_selectedBackendId);
            var availability = AgentStatusService.GetStatus(preset).Availability;

            _backendDropdown.EnableInClassList("agent-pane-backend--ready", availability == AgentAvailability.Ready);
            _backendDropdown.EnableInClassList("agent-pane-backend--warning",
                availability is AgentAvailability.NotInstalled or AgentAvailability.AuthRequired);
            _backendDropdown.EnableInClassList("agent-pane-backend--probing",
                availability is AgentAvailability.Probing or AgentAvailability.Unknown);

            _backendDropdown.tooltip = availability switch
            {
                AgentAvailability.NotInstalled =>
                    $"{preset.Label} is not installed — open CONFIG > AGENTS and use INSTALL.",
                AgentAvailability.AuthRequired =>
                    $"{preset.Label} needs sign-in — open CONFIG > AGENTS and use LOGIN.",
                _ => "Agent backend for this pane. ● ready · ◌ checking · ⚠ needs setup. Switching starts a fresh session.",
            };
        }

        private void ToggleConfigRow()
        {
            var visible = _configRow.resolvedStyle.display != DisplayStyle.None;
            _configRow.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            if (!visible)
                ScheduleConfigRowLayout();
        }

        private void ScheduleConfigRowLayout()
            => _configRow.schedule.Execute(ApplyConfigRowLayout).ExecuteLater(0);

        private void ApplyConfigRowLayout()
        {
            if (_configRow.resolvedStyle.display == DisplayStyle.None)
                return;

            float rowWidth = _configRow.resolvedStyle.width;
            if (rowWidth <= 1f || float.IsNaN(rowWidth))
                return;

            float padding = _configRow.resolvedStyle.paddingLeft + _configRow.resolvedStyle.paddingRight;
            bool modelVisible = _modelDropdown.resolvedStyle.display != DisplayStyle.None;
            bool modeVisible = _modeDropdown.resolvedStyle.display != DisplayStyle.None;
            bool effortVisible = _effortDropdown.resolvedStyle.display != DisplayStyle.None;
            float backendShare = ConfigBackendDesignWidth;
            float modelShare = modelVisible ? ConfigModelDesignWidth : 0f;
            float modeShare = modeVisible ? ConfigModeDesignWidth : 0f;
            float effortShare = effortVisible ? ConfigEffortDesignWidth : 0f;
            float gapCount = 1f + (modelVisible ? 1f : 0f) + (modeVisible ? 1f : 0f) + (effortVisible ? 1f : 0f);
            float available = rowWidth - padding - ConfigControlGap * gapCount - ConfigUsageMinWidth;
            float dropdownShare = backendShare + modelShare + modeShare + effortShare;

            _backendDropdown.style.width = available * backendShare / dropdownShare;
            _modelDropdown.style.width = modelVisible ? available * modelShare / dropdownShare : 0f;
            _modeDropdown.style.width = modeVisible ? available * modeShare / dropdownShare : 0f;
            _effortDropdown.style.width = effortVisible ? available * effortShare / dropdownShare : 0f;
        }

        public void SetActive(bool active)
            => this.Q<VisualElement>("agent-pane")?.EnableInClassList("agent-pane--active", active);

        public void SetBackendSelection(string backendId)
        {
            _selectedBackendId = AgentBackendCatalog.Get(backendId).Id;
            _backendLabel = AgentBackendCatalog.Get(backendId).Label;
            RebuildBackendChoices();
            ApplySelectedBackendStatusClass();
            RefreshModelChoices(backendId);
            var preset = AgentBackendCatalog.Get(backendId);
            _modeDropdown.style.display =
                AgentBackendCatalog.SupportsModes(preset) ? DisplayStyle.Flex : DisplayStyle.None;
            _effortDropdown.style.display =
                AgentBackendCatalog.SupportsEffort(preset) ? DisplayStyle.Flex : DisplayStyle.None;
            ScheduleConfigRowLayout();
            if (_session != null)
                _nameLabel.text = BuildTitle(_session);
        }

        public void SetModelSelection(string? model)
        {
            var value = string.IsNullOrEmpty(model) ? "default" : model!;
            if (!_modelDropdown.choices.Contains(value))
                _modelDropdown.choices.Add(value);
            _modelDropdown.SetValueWithoutNotify(value);
            ScheduleConfigRowLayout();
        }

        public void SetModeSelection(string? mode)
        {
            var index = IndexOfMode(string.IsNullOrEmpty(mode) ? MatrixSpaceSettings.DefaultAgentMode : mode!);
            _modeDropdown.SetValueWithoutNotify(ModeChoices[index]);
        }

        public void SetEffortSelection(string? effort)
        {
            var value = string.IsNullOrEmpty(effort) ? MatrixSpaceSettings.DefaultEffort : effort!;
            if (!EffortChoices.Contains(value))
                value = MatrixSpaceSettings.DefaultEffort;
            _effortDropdown.SetValueWithoutNotify(value);
        }

        private static int IndexOfMode(string mode)
        {
            var index = Array.IndexOf(MatrixSpaceSettings.AgentModes, mode);
            return index >= 0 ? index : Array.IndexOf(MatrixSpaceSettings.AgentModes, MatrixSpaceSettings.DefaultAgentMode);
        }

        public void RefreshModelChoices(string backendId)
        {
            var preset = AgentBackendCatalog.Get(backendId);
            var models = AgentBackendCatalog.GetModels(preset);
            _modelDropdown.choices = models;
            _modelDropdown.style.display = models.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!models.Contains(_modelDropdown.value))
                _modelDropdown.SetValueWithoutNotify(models[0]);
            ScheduleConfigRowLayout();
        }

        public void Bind(AgentSession session)
        {
            Unbind();
            _session = session;
            session.Changed += OnSessionChanged;

            _transcriptContent.Clear();
            _entryViews.Clear();
            _renderedEntryCount = 0;
            _transcriptContent.Add(_workingIndicator);
            OnSessionChanged(session);
        }

        public void Unbind()
        {
            if (_session != null)
                _session.Changed -= OnSessionChanged;
            _session = null;
        }

        public void SetPromptDraft(string? draft)
            => _promptInput.SetValueWithoutNotify(draft ?? string.Empty);

        private void OnPromptKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.Return && evt.keyCode != UnityEngine.KeyCode.KeypadEnter)
                return;

            evt.StopImmediatePropagation();
#if !UNITY_2023_2_OR_NEWER
            evt.PreventDefault();
#endif

            if (evt.shiftKey)
            {
                InsertPromptLineBreak();
                return;
            }

            SendCurrentPrompt();
        }

        private void InsertPromptLineBreak()
        {
            var prompt = _promptInput.value ?? string.Empty;
            var cursorIndex = Mathf.Clamp(_promptInput.cursorIndex, 0, prompt.Length);
            var selectIndex = Mathf.Clamp(_promptInput.selectIndex, 0, prompt.Length);
            var selectionStart = Mathf.Min(cursorIndex, selectIndex);
            var selectionEnd = Mathf.Max(cursorIndex, selectIndex);

            _promptInput.value = prompt.Remove(selectionStart, selectionEnd - selectionStart)
                .Insert(selectionStart, "\n");

            var nextCursorIndex = selectionStart + 1;
            _promptInput.SelectRange(nextCursorIndex, nextCursorIndex);
        }

        private void SendCurrentPrompt()
        {
            if (_session is not { } session || !session.CanSend)
                return;

            var prompt = _promptInput.value;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            _promptInput.SetValueWithoutNotify(string.Empty);
            PromptDraftChanged?.Invoke(this, string.Empty);
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
            UpdateUsage(session);

            UpdateLed(session.State);
            UpdateWorkingIndicator(session.State);

            var busy = session.State is AgentSessionState.Starting or AgentSessionState.Streaming;
            _sendButton.style.display = busy ? DisplayStyle.None : DisplayStyle.Flex;
            _stopButton.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _promptInput.SetEnabled(session.State != AgentSessionState.Exited);
            _backendDropdown.SetEnabled(!busy);
            _modelDropdown.SetEnabled(!busy);
            _modeDropdown.SetEnabled(!busy);
            _effortDropdown.SetEnabled(!busy);

            SyncTranscript(session);
        }

        private void UpdateUsage(AgentSession session)
        {
            var hasTokens = session.TotalInputTokens > 0 || session.TotalOutputTokens > 0;
            var hasCost = session.TotalCostUsd > 0;

            _costLabel.text = (hasTokens, hasCost) switch
            {
                (true, true) => $"▲{FormatTokens(session.TotalInputTokens)} ▼{FormatTokens(session.TotalOutputTokens)} · ${session.TotalCostUsd:F4}",
                (true, false) => $"▲{FormatTokens(session.TotalInputTokens)} ▼{FormatTokens(session.TotalOutputTokens)}",
                (false, true) => $"${session.TotalCostUsd:F4}",
                _ => string.Empty,
            };

            var preset = AgentBackendCatalog.Get(session.BackendId);
            var reportsTurnUsage = preset.IsClaude || preset.IsCodex;
            if (!reportsTurnUsage)
            {
                _configUsageLabel.text = "usage n/a";
                _configUsageLabel.EnableInClassList("agent-pane-usage-detail--na", true);
                _configUsageLabel.tooltip = "This CLI does not expose per-turn token usage.";
                _costLabel.tooltip = hasCost
                    ? "Accumulated API cost of this session."
                    : "This CLI does not expose per-turn token usage.";
                return;
            }

            _configUsageLabel.EnableInClassList("agent-pane-usage-detail--na", !hasTokens && !hasCost);
            _configUsageLabel.text = (hasTokens, hasCost) switch
            {
                (true, true) => $"TOKENS ▲{FormatTokens(session.TotalInputTokens)} ▼{FormatTokens(session.TotalOutputTokens)} ⟳{FormatTokens(session.TotalCacheReadTokens)} · ${session.TotalCostUsd:F4}",
                (true, false) => $"TOKENS ▲{FormatTokens(session.TotalInputTokens)} ▼{FormatTokens(session.TotalOutputTokens)} ⟳{FormatTokens(session.TotalCacheReadTokens)}",
                (false, true) => $"${session.TotalCostUsd:F4}",
                _ => "no usage yet",
            };

            var breakdown =
                "Session usage\n" +
                $"Input: {session.TotalInputTokens:N0}\n" +
                $"Output: {session.TotalOutputTokens:N0}\n" +
                $"Cache read: {session.TotalCacheReadTokens:N0}\n" +
                $"Cache write: {session.TotalCacheCreationTokens:N0}\n" +
                $"Cost: ${session.TotalCostUsd:F4}";
            _costLabel.tooltip = breakdown;
            _configUsageLabel.tooltip = breakdown;
        }

        /// <summary>842 → "842", 12_340 → "12.3k", 1_234_000 → "1.2M".</summary>
        private static string FormatTokens(long n)
        {
            if (n >= 1_000_000)
                return $"{n / 1_000_000.0:0.#}M";
            if (n >= 1_000)
                return $"{n / 1_000.0:0.#}k";
            return n.ToString();
        }

        private void SyncTranscript(AgentSession session)
        {
            var wasAtBottom = IsScrolledToBottom();

            // Append missing entries…
            for (var i = _renderedEntryCount; i < session.Transcript.Count; i++)
            {
                var view = new TranscriptEntryView(session.Transcript[i]);
                _entryViews.Add(view);
                _transcriptContent.Add(view);
            }
            _renderedEntryCount = session.Transcript.Count;
            _workingIndicator.BringToFront();

            // …and refresh every view that has not settled yet (streaming entries mutate
            // in place, and a thinking view collapses once its entry completes).
            foreach (var view in _entryViews)
            {
                if (!view.IsSettled)
                    view.Refresh();
            }

            if (wasAtBottom)
                _transcriptScroll.schedule.Execute(ScrollToBottom);
        }

        private void UpdateWorkingIndicator(AgentSessionState state)
        {
            var busy = state is AgentSessionState.Starting or AgentSessionState.Streaming;
            _workingIndicator.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;

            if (busy)
            {
                _workingAnimation ??= _workingIndicator.schedule.Execute(AnimateWorkingIndicator).Every(400);
                AnimateWorkingIndicator();
            }
            else if (_workingAnimation != null)
            {
                _workingAnimation.Pause();
                _workingAnimation = null;
                _statusLed.style.opacity = 1f;
            }
        }

        private void AnimateWorkingIndicator()
        {
            _workingDotPhase = (_workingDotPhase + 1) % 4;
            var label = _session?.State == AgentSessionState.Starting ? "ESTABLISHING LINK" : "PROCESSING";
            _workingIndicator.text = "▍ " + label + new string('.', _workingDotPhase);
            // Pulse the header LED in the same tick — UI Toolkit has no USS keyframes.
            _statusLed.style.opacity = _workingDotPhase % 2 == 0 ? 1f : 0.35f;
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
