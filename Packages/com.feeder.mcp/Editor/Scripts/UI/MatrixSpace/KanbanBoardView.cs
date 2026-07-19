#nullable enable

using System;
using System.Collections.Generic;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// MatrixBoard: horizontal kanban with the five BridgeBoard columns. Cards carry a
    /// priority stripe + chips and can be dragged between columns; dispatching to an agent
    /// pane stays an explicit button (the window supplies pane targets + dispatch).
    /// </summary>
    public sealed class KanbanBoardView : VisualElement
    {
        /// <summary>(card, paneId or null for "next free").</summary>
        public event Action<MatrixSpaceTaskStore.TaskCard, string?>? DispatchRequested;

        /// <summary>Supplies the current pane targets: (paneId, label, canSend).</summary>
        public Func<List<(string PaneId, string Label, bool CanSend)>>? PaneTargetsProvider;

        /// <summary>Raised after any card mutation (badges may need updating).</summary>
        public event Action? BoardChanged;

        /// <summary>Raised when a card is clicked (without dragging) — opens the detail modal.</summary>
        public event Action<MatrixSpaceTaskStore.TaskCard>? CardOpenRequested;

        /// <summary>(message, actionLabel, action) — window shows it in the toast layer.</summary>
        public event Action<string, string?, Action?>? ToastRequested;

        private static (string Name, Color Color)[] CoverPalette => CardDetailView.CoverPalette;

        private static readonly (TaskCardStatus Status, string Title, string ClassSuffix)[] Columns =
        {
            (TaskCardStatus.Todo, "TO DO", "todo"),
            (TaskCardStatus.InProgress, "IN PROGRESS", "inprogress"),
            (TaskCardStatus.InReview, "IN REVIEW", "inreview"),
            (TaskCardStatus.Complete, "COMPLETE", "complete"),
            (TaskCardStatus.Cancelled, "CANCELLED", "cancelled"),
        };

        private readonly Label _countLabel;
        private readonly VisualElement _columnsRow;
        private readonly Dictionary<TaskCardStatus, VisualElement> _columnCards = new();
        private readonly Dictionary<TaskCardStatus, VisualElement> _columnRoots = new();
        private readonly Dictionary<TaskCardStatus, Label> _columnCounts = new();

        private bool _newTaskEditorOpen;
        private string _filter = string.Empty;
        private readonly TextField _filterField;
        private readonly KanbanDragController _drag;

        public KanbanBoardView()
        {
            AddToClassList("kanban-board");
            focusable = true;

            var header = new VisualElement();
            header.AddToClassList("kanban-header");

            var title = new Label("BOARD // TASKS");
            title.AddToClassList("kanban-title");
            header.Add(title);

            _countLabel = new Label(string.Empty);
            _countLabel.AddToClassList("kanban-count");
            header.Add(_countLabel);

            var spacer = new VisualElement();
            spacer.AddToClassList("kanban-header-spacer");
            header.Add(spacer);

            _filterField = new TextField { tooltip = "Filter cards (F) — title, label, priority" };
            _filterField.AddToClassList("styled-text-field");
            _filterField.AddToClassList("kanban-filter");
            _filterField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue?.Trim() ?? string.Empty;
                Refresh();
            });
            header.Add(_filterField);

            var refresh = new Button(Refresh) { text = "REFRESH" };
            refresh.AddToClassList("btn-secondary");
            refresh.AddToClassList("btn-compact");
            header.Add(refresh);

            var newTask = new Button(ToggleNewTaskEditor) { text = "+ NEW TASK" };
            newTask.AddToClassList("btn-primary");
            newTask.AddToClassList("btn-compact");
            newTask.AddToClassList("kanban-new-task");
            header.Add(newTask);

            Add(header);

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("kanban-scroll");
            _columnsRow = new VisualElement();
            _columnsRow.AddToClassList("kanban-columns");
            scroll.Add(_columnsRow);
            Add(scroll);

            foreach (var (status, columnTitle, suffix) in Columns)
                BuildColumn(status, columnTitle, suffix);

            _drag = new KanbanDragController(
                this, scroll, _columnRoots, _columnCards, BuildCard, OnDropCommit);
            _drag.CardClicked += card => CardOpenRequested?.Invoke(card);

            RegisterCallback<KeyDownEvent>(OnKeyDown);

            Refresh();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && _drag.DragActive)
            {
                _drag.Cancel();
                evt.StopPropagation();
                return;
            }

            // Shortcuts only when the board itself is focused (not while typing in a field).
            if (evt.target != this)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.N:
                    ToggleNewTaskEditor();
                    evt.StopPropagation();
                    break;
                case KeyCode.F:
                    _filterField.Focus();
                    evt.StopPropagation();
                    break;
                case KeyCode.R:
                    Refresh();
                    evt.StopPropagation();
                    break;
            }
        }

        private void OnDropCommit(MatrixSpaceTaskStore.TaskCard card, TaskCardStatus status, int index)
        {
            if (status != card.Status)
            {
                if (status is TaskCardStatus.Complete or TaskCardStatus.Cancelled or TaskCardStatus.Todo)
                    card.AssignedPaneId = null;
                if (status == TaskCardStatus.Complete)
                    card.Failed = false;
            }
            MatrixSpaceTaskStore.instance.ReorderCard(card, status, index);
            BoardChanged?.Invoke();
            Refresh();
        }

        private void BuildColumn(TaskCardStatus status, string title, string suffix)
        {
            var column = new VisualElement();
            column.AddToClassList("kanban-column");
            column.AddToClassList($"kanban-column-{suffix}");

            var header = new VisualElement();
            header.AddToClassList("kanban-column-header");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("kanban-column-title");
            header.Add(titleLabel);

            var count = new Label("0");
            count.AddToClassList("kanban-column-count");
            header.Add(count);

            column.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("kanban-column-scroll");
            var cards = new VisualElement();
            cards.AddToClassList("kanban-column-cards");
            scroll.Add(cards);
            column.Add(scroll);

            _columnRoots[status] = column;
            _columnCards[status] = cards;
            _columnCounts[status] = count;
            _columnsRow.Add(column);
        }

        public void Refresh()
        {
            var store = MatrixSpaceTaskStore.instance;
            store.MigrateIfNeeded();

            foreach (var cards in _columnCards.Values)
                cards.Clear();
            _newTaskEditorOpen = false;

            var counts = new Dictionary<TaskCardStatus, int>();
            foreach (var (status, cards) in _columnCards)
            {
                foreach (var card in store.TasksInOrder(status))
                {
                    if (!MatchesFilter(card))
                        continue;
                    cards.Add(BuildCard(card));
                    counts[status] = counts.TryGetValue(status, out var n) ? n + 1 : 1;
                }
            }

            foreach (var (status, label) in _columnCounts)
                label.text = (counts.TryGetValue(status, out var n) ? n : 0).ToString();

            _countLabel.text = $"{store.Tasks.Count} tasks";
        }

        private bool MatchesFilter(MatrixSpaceTaskStore.TaskCard card)
        {
            if (string.IsNullOrEmpty(_filter))
                return true;

            if (Contains(card.Title) || Contains(card.Prompt) || Contains(card.Priority.ToString()))
                return true;

            foreach (var labelId in card.LabelIds)
            {
                var label = MatrixSpaceTaskStore.instance.FindLabel(labelId);
                if (label != null && Contains(label.Name))
                    return true;
            }

            return false;

            bool Contains(string text)
                => text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DeleteWithUndo(MatrixSpaceTaskStore.TaskCard card)
        {
            MatrixSpaceTaskStore.instance.RemoveTask(card.Id);
            BoardChanged?.Invoke();
            Refresh();
            ToastRequested?.Invoke($"Deleted '{card.Title}'", "UNDO", () =>
            {
                MatrixSpaceTaskStore.instance.RestoreTask(card);
                BoardChanged?.Invoke();
                Refresh();
            });
        }

        // ── New task editor ──────────────────────────────────────────────

        private void ToggleNewTaskEditor()
        {
            if (_newTaskEditorOpen)
            {
                Refresh();
                return;
            }

            _newTaskEditorOpen = true;

            var editor = new VisualElement();
            editor.AddToClassList("kanban-card");
            editor.AddToClassList("kanban-card-editor");

            var titleField = new TextField { tooltip = "Task title" };
            titleField.AddToClassList("styled-text-field");
            titleField.SetValueWithoutNotify("New task");
            editor.Add(titleField);

            var promptField = new TextField { multiline = true, tooltip = "Prompt dispatched to the agent" };
            promptField.AddToClassList("styled-text-field");
            promptField.AddToClassList("kanban-card-edit-prompt");
            editor.Add(promptField);

            var priorityField = new DropdownField(
                new List<string> { "LOW", "MEDIUM", "HIGH", "CRITICAL" }, 1);
            priorityField.AddToClassList("styled-dropdown");
            priorityField.AddToClassList("kanban-card-edit-priority");
            editor.Add(priorityField);

            var add = new Button(() =>
            {
                var title = titleField.value?.Trim();
                var prompt = promptField.value?.Trim();
                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(prompt))
                {
                    MatrixSpaceTaskStore.instance.AddTask(title!, prompt!,
                        (TaskPriority)priorityField.index);
                    BoardChanged?.Invoke();
                }
                Refresh();
            }) { text = "ADD" };
            add.AddToClassList("btn-primary");
            add.AddToClassList("btn-compact");
            editor.Add(add);

            _columnCards[TaskCardStatus.Todo].Insert(0, editor);
            titleField.Focus();
        }

        // ── Cards ────────────────────────────────────────────────────────

        private VisualElement BuildCard(MatrixSpaceTaskStore.TaskCard card)
        {
            var store = MatrixSpaceTaskStore.instance;
            var root = new VisualElement { tooltip = card.Prompt };
            root.AddToClassList("kanban-card");

            var stripe = new VisualElement();
            stripe.AddToClassList("kanban-card-stripe");
            stripe.AddToClassList($"kanban-stripe-{card.Priority.ToString().ToLowerInvariant()}");
            root.Add(stripe);

            var body = new VisualElement();
            body.AddToClassList("kanban-card-body");

            if (card.HasCover)
            {
                var cover = new VisualElement();
                cover.AddToClassList("kanban-card-cover");
                cover.style.backgroundColor = card.CoverColor;
                body.Add(cover);
            }

            if (card.LabelIds.Count > 0)
            {
                var pills = new VisualElement();
                pills.AddToClassList("kanban-card-labels");
                foreach (var labelId in card.LabelIds)
                {
                    var label = store.FindLabel(labelId);
                    if (label == null)
                        continue;
                    var pill = new VisualElement { tooltip = label.Name };
                    pill.AddToClassList("kanban-label-pill");
                    pill.style.backgroundColor = label.Color;
                    pills.Add(pill);
                }
                body.Add(pills);
            }

            var title = new Label(card.Title);
            title.AddToClassList("kanban-card-title");
            body.Add(title);

            var chips = new VisualElement();
            chips.AddToClassList("kanban-card-chips");

            var priorityChip = new Label(card.Priority.ToString().ToUpperInvariant());
            priorityChip.AddToClassList("kanban-chip");
            priorityChip.AddToClassList($"kanban-chip-{card.Priority.ToString().ToLowerInvariant()}");
            chips.Add(priorityChip);

            if (card.DueTicksUtc != 0)
                chips.Add(BuildDueBadge(card));

            if (card.ChecklistItemCount > 0)
            {
                var progress = new Label($"{card.ChecklistDoneCount}/{card.ChecklistItemCount} ✓");
                progress.AddToClassList("kanban-chip");
                if (card.ChecklistDoneCount == card.ChecklistItemCount)
                    progress.AddToClassList("kanban-chip-running");
                chips.Add(progress);
            }

            if (!string.IsNullOrEmpty(card.Description))
            {
                var desc = new Label("≡") { tooltip = "Has description" };
                desc.AddToClassList("kanban-chip");
                chips.Add(desc);
            }

            if (card.Comments.Count > 0)
            {
                var comments = new Label($"🗨 {card.Comments.Count}") { tooltip = "Comments" };
                comments.AddToClassList("kanban-chip");
                chips.Add(comments);
            }

            if (card.Attachments.Count > 0)
            {
                var attachments = new Label($"📎 {card.Attachments.Count}") { tooltip = "Attachments" };
                attachments.AddToClassList("kanban-chip");
                chips.Add(attachments);
            }

            foreach (var definition in store.CustomFields)
            {
                var value = MatrixSpaceTaskStore.GetFieldValue(card, definition.Id);
                if (string.IsNullOrEmpty(value))
                    continue;
                var text = definition.Type switch
                {
                    CustomFieldType.Checkbox => value == "1" ? $"{definition.Name.ToUpperInvariant()} ✓" : null,
                    CustomFieldType.Date => long.TryParse(value, out var ticks) && ticks > 0
                        ? $"{definition.Name.ToUpperInvariant()}: {new DateTime(ticks, DateTimeKind.Utc).ToLocalTime():dd MMM}"
                        : null,
                    _ => $"{definition.Name.ToUpperInvariant()}: {TruncateChip(value)}",
                };
                if (text == null)
                    continue;
                var chip = new Label(text) { tooltip = $"{definition.Name}: {value}" };
                chip.AddToClassList("kanban-chip");
                chips.Add(chip);
            }

            if (!string.IsNullOrEmpty(card.AssignedPaneId))
            {
                var assigned = new Label(ResolvePaneLabel(card.AssignedPaneId!));
                assigned.AddToClassList("kanban-chip");
                assigned.AddToClassList("kanban-chip-assigned");
                chips.Add(assigned);
            }

            if (card.Status == TaskCardStatus.InProgress && !string.IsNullOrEmpty(card.AssignedPaneId))
            {
                var running = new Label("● RUNNING");
                running.AddToClassList("kanban-chip");
                running.AddToClassList("kanban-chip-running");
                chips.Add(running);
            }
            else if (card.Failed)
            {
                var failed = new Label("FAILED");
                failed.AddToClassList("kanban-chip");
                failed.AddToClassList("kanban-chip-failed");
                chips.Add(failed);
            }

            body.Add(chips);

            var buttons = new VisualElement();
            buttons.AddToClassList("kanban-card-buttons");

            if (card.Status == TaskCardStatus.Todo)
            {
                var dispatch = new Button(() => ShowDispatchMenu(card)) { text = "DISPATCH" };
                dispatch.AddToClassList("btn-primary");
                dispatch.AddToClassList("btn-compact");
                buttons.Add(dispatch);
            }
            else if (card.Status == TaskCardStatus.InReview)
            {
                var approve = new Button(() => SetStatus(card, TaskCardStatus.Complete)) { text = "APPROVE" };
                approve.AddToClassList("btn-primary");
                approve.AddToClassList("btn-compact");
                buttons.Add(approve);
            }

            var delete = new Button(() => DeleteWithUndo(card)) { text = "✕", tooltip = "Delete task" };
            delete.AddToClassList("btn-tertiary");
            buttons.Add(delete);

            body.Add(buttons);
            root.Add(body);

            root.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Open", _ => CardOpenRequested?.Invoke(card));
                evt.menu.AppendAction("Cancel task",
                    _ => SetStatus(card, TaskCardStatus.Cancelled),
                    card.Status is TaskCardStatus.Complete or TaskCardStatus.Cancelled
                        ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                foreach (TaskPriority priority in Enum.GetValues(typeof(TaskPriority)))
                {
                    var p = priority;
                    evt.menu.AppendAction($"Priority/{p}", _ =>
                    {
                        card.Priority = p;
                        MatrixSpaceTaskStore.instance.Persist();
                        Refresh();
                    }, card.Priority == p
                        ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                foreach (var boardLabel in MatrixSpaceTaskStore.instance.Labels)
                {
                    var l = boardLabel;
                    evt.menu.AppendAction($"Labels/{l.Name}", _ =>
                    {
                        if (!card.LabelIds.Remove(l.Id))
                            card.LabelIds.Add(l.Id);
                        MatrixSpaceTaskStore.instance.Persist();
                        Refresh();
                    }, card.LabelIds.Contains(l.Id)
                        ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                for (var i = 0; i < CoverPalette.Length; i++)
                {
                    var c = CoverPalette[i].Color;
                    evt.menu.AppendAction($"Cover/{CoverPalette[i].Name}", _ =>
                    {
                        card.HasCover = true;
                        card.CoverColor = c;
                        MatrixSpaceTaskStore.instance.Persist();
                        Refresh();
                    }, card.HasCover && card.CoverColor == c
                        ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                evt.menu.AppendAction("Cover/None", _ =>
                {
                    card.HasCover = false;
                    MatrixSpaceTaskStore.instance.Persist();
                    Refresh();
                }, card.HasCover
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Due/Today", _ => SetDue(card, DateTime.Now));
                evt.menu.AppendAction("Due/+1 day", _ => SetDue(card, DateTime.Now.AddDays(1)));
                evt.menu.AppendAction("Due/+1 week", _ => SetDue(card, DateTime.Now.AddDays(7)));
                evt.menu.AppendAction("Due/Clear", _ => SetDue(card, null),
                    card.DueTicksUtc == 0
                        ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Delete", _ => DeleteWithUndo(card));
            }));

            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.target is Button)
                    return;
                _drag.OnCardPointerDown(card, root, evt);
            });

            return root;
        }

        private VisualElement BuildDueBadge(MatrixSpaceTaskStore.TaskCard card)
        {
            var due = new DateTime(card.DueTicksUtc, DateTimeKind.Utc).ToLocalTime();
            var badge = new Label($"⏱ {due:dd MMM}") { tooltip = $"Due {due:yyyy-MM-dd HH:mm}" };
            badge.AddToClassList("kanban-chip");
            badge.AddToClassList("kanban-due");
            if (card.DueComplete)
                badge.AddToClassList("kanban-due-done");
            else if (due < DateTime.Now)
                badge.AddToClassList("kanban-due-overdue");
            else if (due < DateTime.Now.AddHours(24))
                badge.AddToClassList("kanban-due-soon");
            return badge;
        }

        private void SetDue(MatrixSpaceTaskStore.TaskCard card, DateTime? local)
        {
            card.DueTicksUtc = local == null
                ? 0 : local.Value.Date.AddHours(18).ToUniversalTime().Ticks;
            var store = MatrixSpaceTaskStore.instance;
            store.LogActivity(card, "due-set",
                card.DueTicksUtc == 0 ? "cleared" : local!.Value.ToString("yyyy-MM-dd"));
            store.Persist();
            Refresh();
        }

        private static string TruncateChip(string value)
            => value.Length <= 12 ? value : value[..12] + "…";

        private string ResolvePaneLabel(string paneId)
        {
            var targets = PaneTargetsProvider?.Invoke();
            if (targets != null)
            {
                foreach (var (id, label, _) in targets)
                {
                    if (id == paneId)
                        return label.ToUpperInvariant();
                }
            }
            return "ASSIGNED";
        }

        private void SetStatus(MatrixSpaceTaskStore.TaskCard card, TaskCardStatus status)
        {
            if (status is TaskCardStatus.Complete or TaskCardStatus.Cancelled or TaskCardStatus.Todo)
                card.AssignedPaneId = null;
            if (status == TaskCardStatus.Complete)
                card.Failed = false;
            MatrixSpaceTaskStore.instance.ReorderCard(card, status, int.MaxValue);
            BoardChanged?.Invoke();
            Refresh();
        }

        private void ShowDispatchMenu(MatrixSpaceTaskStore.TaskCard card)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Next free pane"), false,
                () => DispatchRequested?.Invoke(card, null));

            var targets = PaneTargetsProvider?.Invoke() ?? new List<(string, string, bool)>();
            foreach (var (paneId, label, canSend) in targets)
            {
                var content = new GUIContent(label + (canSend ? "" : " (busy)"));
                if (canSend)
                {
                    var id = paneId;
                    menu.AddItem(content, false, () => DispatchRequested?.Invoke(card, id));
                }
                else
                {
                    menu.AddDisabledItem(content);
                }
            }

            menu.ShowAsContext();
        }

    }
}
