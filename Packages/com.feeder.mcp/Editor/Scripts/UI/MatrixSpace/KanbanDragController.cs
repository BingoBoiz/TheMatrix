#nullable enable

using System;
using System.Collections.Generic;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Trello-style pointer drag-and-drop for the kanban board: a live clone of the card
    /// follows the pointer, a dashed placeholder marks the insertion slot (same-column
    /// reorder included), and the board auto-scrolls when dragging near the edges.
    /// The owner element forwards its pointer events here.
    /// </summary>
    public sealed class KanbanDragController
    {
        private const float ActivateDistanceSq = 25f;
        private const float EdgeZone = 44f;
        private const float MaxScrollStep = 14f;

        private readonly VisualElement _owner;
        private readonly ScrollView _horizontalScroll;
        private readonly IReadOnlyDictionary<TaskCardStatus, VisualElement> _columnRoots;
        private readonly IReadOnlyDictionary<TaskCardStatus, VisualElement> _columnCards;
        private readonly Func<MatrixSpaceTaskStore.TaskCard, VisualElement> _cardFactory;
        private readonly Action<MatrixSpaceTaskStore.TaskCard, TaskCardStatus, int> _commit;

        /// <summary>Pointer released without the drag ever activating → treat as a click.</summary>
        public event Action<MatrixSpaceTaskStore.TaskCard>? CardClicked;

        private MatrixSpaceTaskStore.TaskCard? _card;
        private VisualElement? _source;
        private VisualElement? _ghost;
        private VisualElement? _placeholder;
        private int _pointerId = -1;
        private Vector3 _pressPosition;
        private Vector2 _lastPointer;
        private bool _active;
        private float _sourceWidth;
        private float _sourceHeight;
        private TaskCardStatus? _hoverStatus;
        private int _insertIndex;
        private IVisualElementScheduledItem? _autoScroll;

        public bool DragActive => _active;

        public KanbanDragController(
            VisualElement owner,
            ScrollView horizontalScroll,
            IReadOnlyDictionary<TaskCardStatus, VisualElement> columnRoots,
            IReadOnlyDictionary<TaskCardStatus, VisualElement> columnCards,
            Func<MatrixSpaceTaskStore.TaskCard, VisualElement> cardFactory,
            Action<MatrixSpaceTaskStore.TaskCard, TaskCardStatus, int> commit)
        {
            _owner = owner;
            _horizontalScroll = horizontalScroll;
            _columnRoots = columnRoots;
            _columnCards = columnCards;
            _cardFactory = cardFactory;
            _commit = commit;

            _owner.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _owner.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _owner.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
            _owner.RegisterCallback<DetachFromPanelEvent>(_ => Cancel());
        }

        public void OnCardPointerDown(
            MatrixSpaceTaskStore.TaskCard card, VisualElement source, PointerDownEvent evt)
        {
            _card = card;
            _source = source;
            _pointerId = evt.pointerId;
            _pressPosition = evt.position;
            _active = false;
        }

        public void Cancel()
        {
            if (_pointerId >= 0 && _owner.HasPointerCapture(_pointerId))
                _owner.ReleasePointer(_pointerId);
            _autoScroll?.Pause();
            _autoScroll = null;
            if (_source != null)
                _source.style.display = DisplayStyle.Flex;
            _ghost?.RemoveFromHierarchy();
            _placeholder?.RemoveFromHierarchy();
            foreach (var column in _columnRoots.Values)
                column.RemoveFromClassList("kanban-drop-target");
            _card = null;
            _source = null;
            _ghost = null;
            _placeholder = null;
            _pointerId = -1;
            _active = false;
            _hoverStatus = null;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_card == null || _source == null || evt.pointerId != _pointerId)
                return;

            _lastPointer = new Vector2(evt.position.x, evt.position.y);

            if (!_active)
            {
                if ((evt.position - _pressPosition).sqrMagnitude < ActivateDistanceSq)
                    return;
                Activate();
            }

            PositionGhost();
            UpdateDropTarget();
        }

        private void Activate()
        {
            if (_card == null || _source == null)
                return;

            _active = true;
            _owner.CapturePointer(_pointerId);

            // Layout must be captured before the source is hidden (hidden = zero rect).
            _sourceWidth = _source.layout.width;
            _sourceHeight = _source.layout.height;

            _ghost = _cardFactory(_card);
            _ghost.AddToClassList("kanban-card");
            _ghost.AddToClassList("kanban-card-drag-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            foreach (var child in _ghost.Children())
                child.pickingMode = PickingMode.Ignore;
            _ghost.style.width = _sourceWidth;
            _owner.Add(_ghost);

            _placeholder = new VisualElement();
            _placeholder.AddToClassList("kanban-card-placeholder");
            _placeholder.style.height = _sourceHeight;

            // Start with the placeholder in the source slot.
            var parent = _source.parent;
            parent.Insert(parent.IndexOf(_source), _placeholder);
            _source.style.display = DisplayStyle.None;
            _hoverStatus = _card.Status;
            _insertIndex = CardIndexOf(_placeholder);

            _autoScroll = _owner.schedule.Execute(AutoScrollStep).Every(16);
        }

        private void PositionGhost()
        {
            if (_ghost == null)
                return;
            var local = _owner.WorldToLocal(_lastPointer);
            _ghost.style.left = local.x - _sourceWidth * 0.5f;
            _ghost.style.top = local.y + 10f;
        }

        private void UpdateDropTarget()
        {
            TaskCardStatus? hover = null;
            foreach (var (status, column) in _columnRoots)
            {
                var hit = column.worldBound.Contains(_lastPointer);
                column.EnableInClassList("kanban-drop-target", hit);
                if (hit)
                    hover = status;
            }

            if (hover == null)
            {
                _hoverStatus = null;
                _placeholder?.RemoveFromHierarchy();
                return;
            }

            var cards = _columnCards[hover.Value];
            var index = ComputeInsertIndex(cards);
            if (hover == _hoverStatus && index == _insertIndex && _placeholder?.parent == cards)
                return;

            _hoverStatus = hover;
            _insertIndex = index;
            MovePlaceholder(cards, index);
        }

        /// <summary>Insertion slot among the visible cards, from the pointer's Y position.</summary>
        private int ComputeInsertIndex(VisualElement cards)
        {
            var index = 0;
            foreach (var child in cards.Children())
            {
                if (!IsRealCard(child))
                    continue;
                if (_lastPointer.y > child.worldBound.center.y)
                    index++;
            }
            return index;
        }

        private void MovePlaceholder(VisualElement cards, int cardIndex)
        {
            if (_placeholder == null)
                return;
            _placeholder.RemoveFromHierarchy();

            // Map the card slot back to a child index (skipping editor/hidden source).
            var seen = 0;
            var childIndex = cards.childCount;
            for (var i = 0; i < cards.childCount; i++)
            {
                if (!IsRealCard(cards[i]))
                    continue;
                if (seen == cardIndex)
                {
                    childIndex = i;
                    break;
                }
                seen++;
            }
            cards.Insert(childIndex, _placeholder);
        }

        private int CardIndexOf(VisualElement element)
        {
            var parent = element.parent;
            var index = 0;
            foreach (var child in parent.Children())
            {
                if (child == element)
                    return index;
                if (IsRealCard(child))
                    index++;
            }
            return index;
        }

        private bool IsRealCard(VisualElement child)
            => child != _placeholder
               && child != _source
               && child.ClassListContains("kanban-card")
               && !child.ClassListContains("kanban-card-editor");

        private void AutoScrollStep()
        {
            if (!_active)
                return;

            ScrollEdge(_horizontalScroll, horizontal: true);
            if (_hoverStatus != null)
            {
                var columnScroll = _columnRoots[_hoverStatus.Value].Q<ScrollView>();
                if (columnScroll != null)
                    ScrollEdge(columnScroll, horizontal: false);
            }

            PositionGhost();
        }

        private void ScrollEdge(ScrollView scroll, bool horizontal)
        {
            var bound = scroll.worldBound;
            var pos = horizontal ? _lastPointer.x : _lastPointer.y;
            var min = horizontal ? bound.xMin : bound.yMin;
            var max = horizontal ? bound.xMax : bound.yMax;
            if (max - min < EdgeZone * 2f)
                return;

            var delta = 0f;
            if (pos < min + EdgeZone)
                delta = -MaxScrollStep * (1f - (pos - min) / EdgeZone);
            else if (pos > max - EdgeZone)
                delta = MaxScrollStep * (1f - (max - pos) / EdgeZone);
            if (Mathf.Approximately(delta, 0f))
                return;

            var offset = scroll.scrollOffset;
            var viewport = scroll.contentViewport.layout;
            var content = scroll.contentContainer.layout;
            if (horizontal)
                offset.x = Mathf.Clamp(offset.x + delta, 0f, Mathf.Max(0f, content.width - viewport.width));
            else
                offset.y = Mathf.Clamp(offset.y + delta, 0f, Mathf.Max(0f, content.height - viewport.height));
            scroll.scrollOffset = offset;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_card == null || evt.pointerId != _pointerId)
                return;

            var card = _card;
            var wasActive = _active;
            var status = _hoverStatus;
            var index = _insertIndex;
            var hadPlaceholder = _placeholder?.parent != null;
            Cancel();

            if (!wasActive)
            {
                CardClicked?.Invoke(card);
                return;
            }

            if (status != null && hadPlaceholder)
                _commit(card, status.Value, index);
        }
    }
}
