#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Feeder.MCP.Editor.MatrixSpace
{
    public enum TaskCardStatus
    {
        // Legacy values (schema v1) — kept so old serialized assets still deserialize;
        // MigrateIfNeeded maps them to the kanban statuses below.
        Backlog = 0,
        Dispatched = 1,
        Done = 2,
        Failed = 3,

        // Kanban statuses (schema v2), mirroring the five board columns.
        Todo = 10,
        InProgress = 11,
        InReview = 12,
        Complete = 13,
        Cancelled = 14,
    }

    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3,
    }

    public enum CustomFieldType
    {
        Text = 0,
        Number = 1,
        Checkbox = 2,
        Date = 3,
        Dropdown = 4,
    }

    /// <summary>
    /// Task board persistence (MatrixBoard-lite). FilePath-backed ScriptableSingleton:
    /// survives domain reloads AND editor restarts (saved to UserSettings/).
    /// </summary>
    [FilePath("UserSettings/MatrixSpaceTasks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MatrixSpaceTaskStore : ScriptableSingleton<MatrixSpaceTaskStore>
    {
        [Serializable]
        public sealed class ChecklistItem
        {
            public string Id = string.Empty;
            public string Text = string.Empty;
            public bool Done;
        }

        [Serializable]
        public sealed class Checklist
        {
            public string Id = string.Empty;
            public string Title = string.Empty;
            public List<ChecklistItem> Items = new();
        }

        [Serializable]
        public sealed class CardComment
        {
            public string Id = string.Empty;
            public long TicksUtc;
            public string Author = string.Empty;
            public string Text = string.Empty;
        }

        [Serializable]
        public sealed class ActivityEntry
        {
            public long TicksUtc;
            /// <summary>created / moved / dispatched / agent-finished / agent-failed / commented / due-set …</summary>
            public string Kind = string.Empty;
            public string Detail = string.Empty;
        }

        [Serializable]
        public sealed class BoardLabel
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public Color Color = UnityEngine.Color.green;
        }

        [Serializable]
        public sealed class Attachment
        {
            public string Id = string.Empty;
            public string Path = string.Empty;
            public bool IsUrl;
        }

        /// <summary>Board-level custom field definition (Trello-style); cards store values by FieldId.</summary>
        [Serializable]
        public sealed class CustomFieldDefinition
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public CustomFieldType Type = CustomFieldType.Text;
            /// <summary>Choices — only used when Type is Dropdown.</summary>
            public List<string> Options = new();
        }

        /// <summary>
        /// Per-card value, string-serialized by field type: Number = invariant double,
        /// Checkbox = "1"/"0", Date = UTC ticks, Text/Dropdown = raw string.
        /// </summary>
        [Serializable]
        public sealed class CustomFieldValue
        {
            public string FieldId = string.Empty;
            public string Value = string.Empty;
        }

        [Serializable]
        public sealed class TaskCard
        {
            public string Id = string.Empty;
            public string Title = string.Empty;
            public string Prompt = string.Empty;
            public TaskCardStatus Status = TaskCardStatus.Todo;
            public TaskPriority Priority = TaskPriority.Medium;
            /// <summary>Last dispatch of this card ended in an error (shown as a FAILED chip).</summary>
            public bool Failed;
            public string? AssignedPaneId;
            /// <summary>Transcript length of the target session at dispatch (turn marker).</summary>
            public int DispatchMarker;

            // ── Schema v3 (Trello-style detail) — additive, defaults are "unset" ──
            public string Description = string.Empty;
            public List<Checklist> Checklists = new();
            /// <summary>UTC ticks; 0 = no due date.</summary>
            public long DueTicksUtc;
            /// <summary>UTC ticks; 0 = no start date.</summary>
            public long StartTicksUtc;
            public bool DueComplete;
            public List<string> LabelIds = new();
            /// <summary>Position within the column (lower = higher on the board).</summary>
            public int SortIndex;
            public List<CardComment> Comments = new();
            public List<ActivityEntry> Activity = new();
            public long CreatedTicksUtc;
            public bool HasCover;
            public Color CoverColor = UnityEngine.Color.green;
            public List<Attachment> Attachments = new();
            public List<CustomFieldValue> CustomFieldValues = new();

            public int ChecklistItemCount => Checklists.Sum(c => c.Items.Count);
            public int ChecklistDoneCount => Checklists.Sum(c => c.Items.Count(i => i.Done));
        }

        public List<TaskCard> Tasks = new();
        /// <summary>Shared board-wide label set (cards reference labels by id).</summary>
        public List<BoardLabel> Labels = new();
        /// <summary>Board-wide custom field definitions (cards store values by FieldId).</summary>
        public List<CustomFieldDefinition> CustomFields = new();
        public int SchemaVersion;

        private const int CurrentSchemaVersion = 3;
        private const int MaxActivityEntries = 100;

        /// <summary>Maps legacy statuses/fields onto the current schema. Safe to call repeatedly.</summary>
        public void MigrateIfNeeded()
        {
            if (SchemaVersion >= CurrentSchemaVersion)
                return;

            if (SchemaVersion < 2)
            {
                foreach (var card in Tasks)
                {
                    card.Status = card.Status switch
                    {
                        TaskCardStatus.Backlog => TaskCardStatus.Todo,
                        TaskCardStatus.Dispatched => TaskCardStatus.InProgress,
                        TaskCardStatus.Done => TaskCardStatus.Complete,
                        TaskCardStatus.Failed => MarkFailed(card),
                        _ => card.Status,
                    };
                }
            }

            if (SchemaVersion < 3)
            {
                // Stable per-column ordering for pre-v3 cards + creation timestamps.
                var perStatus = new Dictionary<TaskCardStatus, int>();
                foreach (var card in Tasks)
                {
                    perStatus.TryGetValue(card.Status, out var next);
                    card.SortIndex = next;
                    perStatus[card.Status] = next + 1;
                    if (card.CreatedTicksUtc == 0)
                        card.CreatedTicksUtc = DateTime.UtcNow.Ticks;
                }

                if (Labels.Count == 0)
                {
                    Labels.Add(NewLabel("BUG", new Color(1f, 0.47f, 0.35f)));
                    Labels.Add(NewLabel("FEATURE", new Color(0.24f, 1f, 0.39f)));
                    Labels.Add(NewLabel("REFACTOR", new Color(1f, 0.82f, 0.39f)));
                    Labels.Add(NewLabel("DOCS", new Color(0.35f, 0.67f, 0.43f)));
                    Labels.Add(NewLabel("URGENT", new Color(1f, 0.35f, 0.55f)));
                }
            }

            SchemaVersion = CurrentSchemaVersion;
            Persist();

            static TaskCardStatus MarkFailed(TaskCard card)
            {
                card.Failed = true;
                return TaskCardStatus.Todo;
            }

            static BoardLabel NewLabel(string name, Color color) => new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Color = color,
            };
        }

        public TaskCard AddTask(string title, string prompt, TaskPriority priority = TaskPriority.Medium)
        {
            var card = new TaskCard
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Prompt = prompt,
                Priority = priority,
                CreatedTicksUtc = DateTime.UtcNow.Ticks,
                SortIndex = NextSortIndex(TaskCardStatus.Todo),
            };
            Tasks.Add(card);
            LogActivity(card, "created", title);
            Persist();
            return card;
        }

        public void RemoveTask(string id)
        {
            Tasks.RemoveAll(t => t.Id == id);
            Persist();
        }

        /// <summary>Reinserts a previously removed card (toast UNDO path).</summary>
        public void RestoreTask(TaskCard card)
        {
            if (Tasks.Any(t => t.Id == card.Id))
                return;
            Tasks.Add(card);
            Persist();
        }

        /// <summary>Cards of one column in board order.</summary>
        public IEnumerable<TaskCard> TasksInOrder(TaskCardStatus status)
            => Tasks.Where(t => t.Status == status).OrderBy(t => t.SortIndex);

        /// <summary>
        /// Moves a card to <paramref name="newStatus"/> at <paramref name="insertIndex"/>
        /// (position among the target column's cards) and renumbers both columns.
        /// </summary>
        public void ReorderCard(TaskCard card, TaskCardStatus newStatus, int insertIndex)
        {
            var oldStatus = card.Status;
            var target = TasksInOrder(newStatus).Where(t => t != card).ToList();
            insertIndex = Mathf.Clamp(insertIndex, 0, target.Count);
            target.Insert(insertIndex, card);
            card.Status = newStatus;
            for (var i = 0; i < target.Count; i++)
                target[i].SortIndex = i;

            if (oldStatus != newStatus)
            {
                var source = TasksInOrder(oldStatus).ToList();
                for (var i = 0; i < source.Count; i++)
                    source[i].SortIndex = i;
                LogActivity(card, "moved", $"{oldStatus} → {newStatus}");
            }

            Persist();
        }

        public void LogActivity(TaskCard card, string kind, string detail)
        {
            card.Activity.Add(new ActivityEntry
            {
                TicksUtc = DateTime.UtcNow.Ticks,
                Kind = kind,
                Detail = detail,
            });
            if (card.Activity.Count > MaxActivityEntries)
                card.Activity.RemoveRange(0, card.Activity.Count - MaxActivityEntries);
        }

        public BoardLabel? FindLabel(string id) => Labels.FirstOrDefault(l => l.Id == id);

        public BoardLabel AddLabel(string name, Color color)
        {
            var label = new BoardLabel
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Color = color,
            };
            Labels.Add(label);
            Persist();
            return label;
        }

        public void RemoveLabel(string id)
        {
            Labels.RemoveAll(l => l.Id == id);
            foreach (var card in Tasks)
                card.LabelIds.Remove(id);
            Persist();
        }

        // ── Custom fields ────────────────────────────────────────────────

        public CustomFieldDefinition AddCustomField(string name, CustomFieldType type, List<string>? options = null)
        {
            var definition = new CustomFieldDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Type = type,
                Options = options ?? new List<string>(),
            };
            CustomFields.Add(definition);
            Persist();
            return definition;
        }

        public void RemoveCustomField(string id)
        {
            CustomFields.RemoveAll(f => f.Id == id);
            foreach (var card in Tasks)
                card.CustomFieldValues.RemoveAll(v => v.FieldId == id);
            Persist();
        }

        public static string GetFieldValue(TaskCard card, string fieldId)
            => card.CustomFieldValues.FirstOrDefault(v => v.FieldId == fieldId)?.Value ?? string.Empty;

        /// <summary>Upserts the value; an empty value removes the entry. Does not persist.</summary>
        public static void SetFieldValue(TaskCard card, string fieldId, string value)
        {
            var existing = card.CustomFieldValues.FirstOrDefault(v => v.FieldId == fieldId);
            if (string.IsNullOrEmpty(value))
            {
                if (existing != null)
                    card.CustomFieldValues.Remove(existing);
                return;
            }

            if (existing != null)
                existing.Value = value;
            else
                card.CustomFieldValues.Add(new CustomFieldValue { FieldId = fieldId, Value = value });
        }

        private int NextSortIndex(TaskCardStatus status)
        {
            var max = -1;
            foreach (var task in Tasks)
            {
                if (task.Status == status && task.SortIndex > max)
                    max = task.SortIndex;
            }
            return max + 1;
        }

        public void Persist() => Save(true);
    }
}
