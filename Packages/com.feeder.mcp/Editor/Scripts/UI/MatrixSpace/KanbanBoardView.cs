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

        // Drag state.
        private MatrixSpaceTaskStore.TaskCard? _dragCard;
        private VisualElement? _dragSource;
        private VisualElement? _ghost;
        private int _dragPointerId = -1;
        private Vector3 _dragStart;
        private bool _dragActive;

        public KanbanBoardView()
        {
            AddToClassList("kanban-board");

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

            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());

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
            foreach (var card in store.Tasks)
            {
                if (!_columnCards.TryGetValue(card.Status, out var cards))
                    continue;
                cards.Add(BuildCard(card));
                counts[card.Status] = counts.TryGetValue(card.Status, out var n) ? n + 1 : 1;
            }

            foreach (var (status, label) in _columnCounts)
                label.text = (counts.TryGetValue(status, out var n) ? n : 0).ToString();

            _countLabel.text = $"{store.Tasks.Count} tasks";
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
            var root = new VisualElement { tooltip = card.Prompt };
            root.AddToClassList("kanban-card");

            var stripe = new VisualElement();
            stripe.AddToClassList("kanban-card-stripe");
            stripe.AddToClassList($"kanban-stripe-{card.Priority.ToString().ToLowerInvariant()}");
            root.Add(stripe);

            var body = new VisualElement();
            body.AddToClassList("kanban-card-body");

            var title = new Label(card.Title);
            title.AddToClassList("kanban-card-title");
            body.Add(title);

            var chips = new VisualElement();
            chips.AddToClassList("kanban-card-chips");

            var priorityChip = new Label(card.Priority.ToString().ToUpperInvariant());
            priorityChip.AddToClassList("kanban-chip");
            priorityChip.AddToClassList($"kanban-chip-{card.Priority.ToString().ToLowerInvariant()}");
            chips.Add(priorityChip);

            if (!string.IsNullOrEmpty(card.AssignedPaneId))
            {
                var assigned = new Label("ASSIGNED");
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

            var delete = new Button(() =>
            {
                MatrixSpaceTaskStore.instance.RemoveTask(card.Id);
                BoardChanged?.Invoke();
                Refresh();
            }) { text = "✕", tooltip = "Delete task" };
            delete.AddToClassList("btn-tertiary");
            buttons.Add(delete);

            body.Add(buttons);
            root.Add(body);

            root.AddManipulator(new ContextualMenuManipulator(evt =>
            {
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
                evt.menu.AppendAction("Delete", _ =>
                {
                    MatrixSpaceTaskStore.instance.RemoveTask(card.Id);
                    BoardChanged?.Invoke();
                    Refresh();
                });
            }));

            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                _dragCard = card;
                _dragSource = root;
                _dragPointerId = evt.pointerId;
                _dragStart = evt.position;
                _dragActive = false;
            });

            return root;
        }

        private void SetStatus(MatrixSpaceTaskStore.TaskCard card, TaskCardStatus status)
        {
            card.Status = status;
            if (status is TaskCardStatus.Complete or TaskCardStatus.Cancelled or TaskCardStatus.Todo)
                card.AssignedPaneId = null;
            if (status == TaskCardStatus.Complete)
                card.Failed = false;
            MatrixSpaceTaskStore.instance.Persist();
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

        // ── Drag and drop ────────────────────────────────────────────────

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_dragCard == null || _dragSource == null || evt.pointerId != _dragPointerId)
                return;

            if (!_dragActive)
            {
                if ((evt.position - _dragStart).sqrMagnitude < 25f)
                    return;

                _dragActive = true;
                this.CapturePointer(_dragPointerId);
                _dragSource.AddToClassList("kanban-card-dragging");

                _ghost = new Label(_dragCard.Title);
                _ghost.AddToClassList("kanban-card-ghost");
                _ghost.pickingMode = PickingMode.Ignore;
                Add(_ghost);
            }

            if (_ghost != null)
            {
                var local = this.WorldToLocal(evt.position);
                _ghost.style.left = local.x + 8;
                _ghost.style.top = local.y + 8;
            }

            foreach (var (status, column) in _columnRoots)
                column.EnableInClassList("kanban-drop-target",
                    column.worldBound.Contains(new Vector2(evt.position.x, evt.position.y)));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_dragCard == null || evt.pointerId != _dragPointerId)
                return;

            var card = _dragCard;
            var wasActive = _dragActive;
            var position = new Vector2(evt.position.x, evt.position.y);
            CancelDrag();

            if (!wasActive)
                return;

            foreach (var (status, column) in _columnRoots)
            {
                if (!column.worldBound.Contains(position) || card.Status == status)
                    continue;
                SetStatus(card, status);
                return;
            }
        }

        private void CancelDrag()
        {
            if (_dragPointerId >= 0 && this.HasPointerCapture(_dragPointerId))
                this.ReleasePointer(_dragPointerId);
            _dragSource?.RemoveFromClassList("kanban-card-dragging");
            _ghost?.RemoveFromHierarchy();
            foreach (var column in _columnRoots.Values)
                column.RemoveFromClassList("kanban-drop-target");
            _dragCard = null;
            _dragSource = null;
            _ghost = null;
            _dragPointerId = -1;
            _dragActive = false;
        }
    }
}
