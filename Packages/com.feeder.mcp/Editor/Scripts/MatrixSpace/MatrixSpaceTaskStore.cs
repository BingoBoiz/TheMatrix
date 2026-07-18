#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

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

    /// <summary>
    /// Task board persistence (MatrixBoard-lite). FilePath-backed ScriptableSingleton:
    /// survives domain reloads AND editor restarts (saved to UserSettings/).
    /// </summary>
    [FilePath("UserSettings/MatrixSpaceTasks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MatrixSpaceTaskStore : ScriptableSingleton<MatrixSpaceTaskStore>
    {
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
        }

        public List<TaskCard> Tasks = new();
        public int SchemaVersion;

        private const int CurrentSchemaVersion = 2;

        /// <summary>Maps legacy v1 statuses onto the kanban columns. Safe to call repeatedly.</summary>
        public void MigrateIfNeeded()
        {
            if (SchemaVersion >= CurrentSchemaVersion)
                return;

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

            SchemaVersion = CurrentSchemaVersion;
            Persist();

            static TaskCardStatus MarkFailed(TaskCard card)
            {
                card.Failed = true;
                return TaskCardStatus.Todo;
            }
        }

        public TaskCard AddTask(string title, string prompt, TaskPriority priority = TaskPriority.Medium)
        {
            var card = new TaskCard
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Prompt = prompt,
                Priority = priority,
            };
            Tasks.Add(card);
            Persist();
            return card;
        }

        public void RemoveTask(string id)
        {
            Tasks.RemoveAll(t => t.Id == id);
            Persist();
        }

        public void Persist() => Save(true);
    }
}
