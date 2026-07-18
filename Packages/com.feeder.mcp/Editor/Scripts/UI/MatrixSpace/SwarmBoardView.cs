#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// MatrixSwarm board: mission bar on top, a radial node canvas (coordinator centered,
    /// role agents around it with connecting lines), and a bottom command bar with quick
    /// actions + an @all/@role input that talks to the live sessions.
    /// </summary>
    public sealed class SwarmBoardView : VisualElement
    {
        private const string TargetAll = "@all";

        public event Action<string, List<SwarmMission.Role>>? LaunchRequested;

        /// <summary>Resolves a paneId to its live session (supplied by the window).</summary>
        public Func<string, AgentSession?>? SessionResolver;

        private sealed class NodeCard
        {
            public MatrixSpaceSessionStore.RoleAssignment Assignment = null!;
            public SwarmMission.Role? Role;
            public AgentSession? Session;
            public VisualElement Root = null!;
            public Label Activity = null!;
            public VisualElement Dot = null!;
            public Action<AgentSession>? ChangedHandler;
        }

        private readonly VisualElement _setupBar;
        private readonly TextField _missionInput;
        private readonly Dictionary<string, Toggle> _roleToggles = new();

        private readonly VisualElement _runningBar;
        private readonly Label _missionLabel;
        private readonly Label _timerLabel;
        private readonly Label _liveLabel;

        private readonly VisualElement _canvas;
        private readonly List<NodeCard> _cards = new();

        private readonly VisualElement _bottomBar;
        private readonly DropdownField _targetField;
        private readonly TextField _commandInput;

        public SwarmBoardView()
        {
            AddToClassList("swarm-board");

            // ── Mission bar: setup mode ──────────────────────────────────
            _setupBar = new VisualElement();
            _setupBar.AddToClassList("swarm-setup-bar");

            var setupTitle = new Label("SWARM MISSION");
            setupTitle.AddToClassList("matrix-settings-title");
            _setupBar.Add(setupTitle);

            _missionInput = new TextField
            {
                multiline = true,
                tooltip = "Describe the mission. Each selected role gets its own pane and charter; they coordinate via MatrixSpace/mailbox/.",
            };
            _missionInput.AddToClassList("styled-text-field");
            _missionInput.AddToClassList("mission-input");
            _setupBar.Add(_missionInput);

            var rolesRow = new VisualElement();
            rolesRow.AddToClassList("mission-roles");
            foreach (var role in SwarmMission.Roles)
            {
                var toggle = new Toggle(role.Label) { value = role.DefaultEnabled, tooltip = role.Charter };
                toggle.AddToClassList("mission-role-toggle");
                rolesRow.Add(toggle);
                _roleToggles[role.Id] = toggle;
            }
            _setupBar.Add(rolesRow);

            var launch = new Button(OnLaunchClicked) { text = "LAUNCH TEAM" };
            launch.AddToClassList("btn-primary");
            launch.AddToClassList("btn-compact");
            launch.AddToClassList("mission-launch");
            _setupBar.Add(launch);

            Add(_setupBar);

            // ── Mission bar: running mode ────────────────────────────────
            _runningBar = new VisualElement();
            _runningBar.AddToClassList("swarm-running-bar");

            var missionTag = new Label("MISSION");
            missionTag.AddToClassList("swarm-running-tag");
            _runningBar.Add(missionTag);

            _missionLabel = new Label(string.Empty);
            _missionLabel.AddToClassList("swarm-running-mission");
            _runningBar.Add(_missionLabel);

            var runningSpacer = new VisualElement();
            runningSpacer.AddToClassList("swarm-running-spacer");
            _runningBar.Add(runningSpacer);

            _liveLabel = new Label(string.Empty);
            _liveLabel.AddToClassList("swarm-running-live");
            _runningBar.Add(_liveLabel);

            _timerLabel = new Label("00:00");
            _timerLabel.AddToClassList("swarm-running-timer");
            _runningBar.Add(_timerLabel);

            var stop = new Button(StopMission) { text = "STOP" };
            stop.AddToClassList("btn-secondary");
            stop.AddToClassList("btn-compact");
            stop.AddToClassList("swarm-stop");
            _runningBar.Add(stop);

            Add(_runningBar);

            // ── Node canvas ──────────────────────────────────────────────
            _canvas = new VisualElement();
            _canvas.AddToClassList("swarm-canvas");
            _canvas.generateVisualContent += DrawEdges;
            _canvas.RegisterCallback<GeometryChangedEvent>(_ => LayoutNodes());
            Add(_canvas);

            // ── Bottom command bar ───────────────────────────────────────
            _bottomBar = new VisualElement();
            _bottomBar.AddToClassList("swarm-bottom");

            var chipsRow = new VisualElement();
            chipsRow.AddToClassList("swarm-chips");
            chipsRow.Add(BuildQuickChip("Status report", "Post a status report to your mailbox now."));
            chipsRow.Add(BuildQuickChip("Wrap up", "Finish your current slice and write a final mailbox summary."));
            chipsRow.Add(BuildQuickChip("Pause work", "Pause after the current step; await further directives."));
            _bottomBar.Add(chipsRow);

            var inputRow = new VisualElement();
            inputRow.AddToClassList("swarm-input-row");

            _targetField = new DropdownField(new List<string> { TargetAll }, 0);
            _targetField.AddToClassList("styled-dropdown");
            _targetField.AddToClassList("swarm-target");
            inputRow.Add(_targetField);

            _commandInput = new TextField { tooltip = "Directive for the selected target(s). Enter to send." };
            _commandInput.AddToClassList("styled-text-field");
            _commandInput.AddToClassList("swarm-command-input");
            _commandInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter && !evt.shiftKey)
                {
                    evt.StopPropagation();
                    SendCommand(_commandInput.value);
                    _commandInput.SetValueWithoutNotify(string.Empty);
                }
            }, TrickleDown.TrickleDown);
            inputRow.Add(_commandInput);

            var send = new Button(() =>
            {
                SendCommand(_commandInput.value);
                _commandInput.SetValueWithoutNotify(string.Empty);
            }) { text = "SEND" };
            send.AddToClassList("btn-primary");
            send.AddToClassList("btn-compact");
            inputRow.Add(send);

            _bottomBar.Add(inputRow);
            Add(_bottomBar);

            schedule.Execute(UpdateTimerAndCounts).Every(1000);

            RefreshFromState();
        }

        // ── State binding ────────────────────────────────────────────────

        public void RefreshFromState()
        {
            var swarm = MatrixSpaceSessionStore.instance.Swarm;
            var running = swarm.Active && swarm.Assignments.Count > 0;

            _setupBar.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            _runningBar.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            _bottomBar.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            _canvas.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;

            UnbindCards();
            _canvas.Clear();
            _cards.Clear();

            if (!running)
                return;

            _missionLabel.text = Truncate(swarm.Mission.Replace('\n', ' '), 80);

            foreach (var assignment in swarm.Assignments)
            {
                var role = SwarmMission.Roles.FirstOrDefault(r => r.Id == assignment.RoleId);
                var session = SessionResolver?.Invoke(assignment.PaneId);
                var card = BuildNodeCard(assignment, role, session);
                _cards.Add(card);
                _canvas.Add(card.Root);
            }

            var targets = new List<string> { TargetAll };
            targets.AddRange(_cards.Select(c => "@" + (c.Role?.Label ?? c.Assignment.RoleId)));
            _targetField.choices = targets;
            _targetField.SetValueWithoutNotify(TargetAll);

            LayoutNodes();
            UpdateTimerAndCounts();
        }

        private NodeCard BuildNodeCard(
            MatrixSpaceSessionStore.RoleAssignment assignment,
            SwarmMission.Role? role,
            AgentSession? session)
        {
            var card = new NodeCard { Assignment = assignment, Role = role, Session = session };

            var root = new VisualElement();
            root.AddToClassList("swarm-node");
            if (role?.Id == "coordinator")
                root.AddToClassList("swarm-node-coordinator");

            var header = new VisualElement();
            header.AddToClassList("swarm-node-header");

            var badge = new Label(RoleBadge(role));
            badge.AddToClassList("swarm-node-badge");
            header.Add(badge);

            var name = new Label(session?.DisplayName ?? assignment.PaneId[..Math.Min(8, assignment.PaneId.Length)]);
            name.AddToClassList("swarm-node-name");
            header.Add(name);

            var dot = new VisualElement();
            dot.AddToClassList("swarm-node-dot");
            header.Add(dot);

            root.Add(header);

            var activity = new Label("—");
            activity.AddToClassList("swarm-node-activity");
            root.Add(activity);

            card.Root = root;
            card.Activity = activity;
            card.Dot = dot;

            if (session != null)
            {
                card.ChangedHandler = _ => UpdateNodeCard(card);
                session.Changed += card.ChangedHandler;
                UpdateNodeCard(card);
            }

            return card;
        }

        private static string RoleBadge(SwarmMission.Role? role) => role?.Id switch
        {
            "coordinator" => "♛ CONTROLLER",
            "reviewer" => "◎ REVIEW",
            "scout" => "🔍 SCOUT",
            null => "AGENT",
            _ => "⚒ BUILD",
        };

        private void UpdateNodeCard(NodeCard card)
        {
            if (card.Session == null)
                return;

            var last = card.Session.Transcript.LastOrDefault();
            card.Activity.text = last != null ? Truncate(last.Text.Replace('\n', ' '), 60) : "—";

            card.Dot.EnableInClassList("swarm-node-dot-busy",
                card.Session.State is AgentSessionState.Starting or AgentSessionState.Streaming);
            card.Dot.EnableInClassList("swarm-node-dot-error",
                card.Session.State is AgentSessionState.Error);
            UpdateTimerAndCounts();
        }

        private void UnbindCards()
        {
            foreach (var card in _cards)
            {
                if (card.Session != null && card.ChangedHandler != null)
                    card.Session.Changed -= card.ChangedHandler;
            }
        }

        // ── Radial layout + edges ────────────────────────────────────────

        private void LayoutNodes()
        {
            var rect = _canvas.contentRect;
            if (rect.width < 50f || rect.height < 50f || _cards.Count == 0)
                return;

            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) / 3f;

            var coordinator = _cards.FirstOrDefault(c => c.Role?.Id == "coordinator");
            var satellites = _cards.Where(c => c != coordinator).ToList();

            if (coordinator != null)
                PlaceNode(coordinator, center);

            for (var i = 0; i < satellites.Count; i++)
            {
                var angle = (Mathf.PI * 2f * i / satellites.Count) - Mathf.PI / 2f;
                var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                PlaceNode(satellites[i], pos);
            }

            _canvas.MarkDirtyRepaint();
        }

        private static void PlaceNode(NodeCard card, Vector2 center)
        {
            const float width = 190f;
            card.Root.style.position = Position.Absolute;
            card.Root.style.width = width;
            card.Root.style.left = center.x - width / 2f;
            card.Root.style.top = center.y - 24f;
        }

        private void DrawEdges(MeshGenerationContext ctx)
        {
            if (_cards.Count < 2)
                return;

            var coordinator = _cards.FirstOrDefault(c => c.Role?.Id == "coordinator") ?? _cards[0];
            var from = NodeCenter(coordinator);

            var painter = ctx.painter2D;
            painter.strokeColor = new Color(0.15f, 0.82f, 0.29f, 0.28f);
            painter.lineWidth = 1f;

            foreach (var card in _cards)
            {
                if (card == coordinator)
                    continue;
                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(NodeCenter(card));
                painter.Stroke();
            }

            static Vector2 NodeCenter(NodeCard card)
            {
                var rect = card.Root.layout;
                return new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            }
        }

        // ── Mission control ──────────────────────────────────────────────

        private void OnLaunchClicked()
        {
            var mission = _missionInput.value?.Trim();
            if (string.IsNullOrEmpty(mission))
                return;

            var activeRoles = SwarmMission.Roles.Where(r => _roleToggles[r.Id].value).ToList();
            if (activeRoles.Count == 0)
                return;

            LaunchRequested?.Invoke(mission!, activeRoles);
            RefreshFromState();
        }

        private void StopMission()
        {
            foreach (var card in _cards)
                card.Session?.Cancel();

            MatrixSpaceSessionStore.instance.Swarm.Active = false;
            RefreshFromState();
        }

        private Button BuildQuickChip(string label, string prompt)
        {
            var chip = new Button(() => SendCommand(prompt)) { text = label };
            chip.AddToClassList("btn-tertiary");
            chip.AddToClassList("swarm-chip");
            return chip;
        }

        private void SendCommand(string? command)
        {
            command = command?.Trim();
            if (string.IsNullOrEmpty(command))
                return;

            var target = _targetField.value;
            foreach (var card in _cards)
            {
                if (card.Session is not { } session)
                    continue;
                var label = "@" + (card.Role?.Label ?? card.Assignment.RoleId);
                if (target != TargetAll && label != target)
                    continue;
                if (session.CanSend)
                    session.Send($"[OPERATOR DIRECTIVE]\n{command}");
            }
        }

        private void UpdateTimerAndCounts()
        {
            var swarm = MatrixSpaceSessionStore.instance.Swarm;
            if (!swarm.Active)
                return;

            var elapsed = DateTime.UtcNow - new DateTime(swarm.StartedTicksUtc, DateTimeKind.Utc);
            if (elapsed.Ticks < 0)
                elapsed = TimeSpan.Zero;
            _timerLabel.text = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
                : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";

            var live = 0;
            var err = 0;
            foreach (var card in _cards)
            {
                switch (card.Session?.State)
                {
                    case AgentSessionState.Starting:
                    case AgentSessionState.Streaming:
                        live++;
                        break;
                    case AgentSessionState.Error:
                        err++;
                        break;
                }
            }
            _liveLabel.text = $"{live} live · {err} err";
        }

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text[..max] + "…";
    }
}
