#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Trello-style card detail modal: scrim + centered panel with title, status/priority,
    /// labels, dates, description, checklists, assignee, comments and activity log.
    /// Attach to the window root via <see cref="Open"/>; every mutation persists the store
    /// and raises <see cref="Changed"/> so the board can refresh behind the modal.
    /// </summary>
    public sealed class CardDetailView : VisualElement
    {
        private static readonly (TaskCardStatus Status, string Label)[] StatusChoices =
        {
            (TaskCardStatus.Todo, "TO DO"),
            (TaskCardStatus.InProgress, "IN PROGRESS"),
            (TaskCardStatus.InReview, "IN REVIEW"),
            (TaskCardStatus.Complete, "COMPLETE"),
            (TaskCardStatus.Cancelled, "CANCELLED"),
        };

        /// <summary>Shared swatch palette for labels and card covers (also used by the board's context menu).</summary>
        internal static readonly (string Name, Color Color)[] CoverPalette =
        {
            ("ORANGE", new Color(1f, 0.47f, 0.35f)),
            ("GREEN", new Color(0.24f, 1f, 0.39f)),
            ("YELLOW", new Color(1f, 0.82f, 0.39f)),
            ("DIM", new Color(0.35f, 0.67f, 0.43f)),
            ("PINK", new Color(1f, 0.35f, 0.55f)),
            ("CYAN", new Color(0.4f, 0.85f, 1f)),
        };

        private static readonly Color[] LabelPalette = CoverPalette.Select(p => p.Color).ToArray();

        public event Action? Changed;
        public event Action? Closed;

        private readonly MatrixSpaceTaskStore.TaskCard _card;
        private readonly Func<List<(string PaneId, string Label, bool CanSend)>>? _paneTargets;
        private readonly VisualElement _panel;
        private readonly VisualElement _labelsRow;
        private readonly VisualElement _checklistsHost;
        private readonly VisualElement _feedHost;
        private VisualElement _fieldsHost = null!;
        private VisualElement _attachmentsHost = null!;

        private static MatrixSpaceTaskStore Store => MatrixSpaceTaskStore.instance;

        public static CardDetailView Open(
            VisualElement host,
            MatrixSpaceTaskStore.TaskCard card,
            Func<List<(string PaneId, string Label, bool CanSend)>>? paneTargets,
            Action? onChanged)
        {
            var view = new CardDetailView(card, paneTargets);
            if (onChanged != null)
                view.Changed += onChanged;
            host.Add(view);
            view._panel.schedule.Execute(() => view._panel.Focus());
            return view;
        }

        private CardDetailView(
            MatrixSpaceTaskStore.TaskCard card,
            Func<List<(string PaneId, string Label, bool CanSend)>>? paneTargets)
        {
            _card = card;
            _paneTargets = paneTargets;

            AddToClassList("kanban-detail-scrim");
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == this)
                {
                    Close();
                    evt.StopPropagation();
                }
            });

            _panel = new VisualElement { focusable = true };
            _panel.AddToClassList("kanban-detail-panel");
            _panel.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            });
            Add(_panel);

            _panel.Add(BuildHeader());

            _labelsRow = new VisualElement();
            _labelsRow.AddToClassList("kanban-detail-labels");
            _panel.Add(_labelsRow);
            RenderLabels();

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("kanban-detail-body");
            _panel.Add(body);

            body.Add(BuildDatesSection());
            body.Add(BuildCoverSection());
            body.Add(BuildDescriptionSection());

            _checklistsHost = new VisualElement();
            body.Add(SectionTitleRow("CHECKLISTS", "+ CHECKLIST", AddChecklist));
            body.Add(_checklistsHost);
            RenderChecklists();

            body.Add(BuildAssigneeSection());

            body.Add(BuildAttachmentsSection());

            _fieldsHost = new VisualElement();
            body.Add(SectionTitleRow("FIELDS", "MANAGE…", ShowFieldManager));
            body.Add(_fieldsHost);
            RenderFields();

            body.Add(SectionTitle("COMMENTS & ACTIVITY"));
            body.Add(BuildCommentComposer());
            _feedHost = new VisualElement();
            _feedHost.AddToClassList("kanban-detail-feed");
            body.Add(_feedHost);
            RenderFeed();
        }

        public void Close()
        {
            RemoveFromHierarchy();
            Closed?.Invoke();
        }

        private void NotifyChanged()
        {
            Store.Persist();
            Changed?.Invoke();
        }

        // ── Header (title / status / priority / labels button / close) ───

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("kanban-detail-header");

            var titleHost = new VisualElement();
            titleHost.AddToClassList("kanban-detail-title-host");

            var title = new Label(_card.Title) { tooltip = "Click to rename" };
            title.AddToClassList("kanban-detail-title");
            title.RegisterCallback<ClickEvent>(_ =>
            {
                var field = new TextField { value = _card.Title };
                field.AddToClassList("styled-text-field");
                field.AddToClassList("kanban-detail-title-field");

                void Commit()
                {
                    var value = field.value?.Trim();
                    if (!string.IsNullOrEmpty(value) && value != _card.Title)
                    {
                        _card.Title = value!;
                        NotifyChanged();
                    }
                    title.text = _card.Title;
                    if (field.parent == titleHost)
                        titleHost.Remove(field);
                    title.style.display = DisplayStyle.Flex;
                }

                field.RegisterCallback<FocusOutEvent>(_ => Commit());
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        Commit();
                        evt.StopPropagation();
                    }
                });
                title.style.display = DisplayStyle.None;
                titleHost.Add(field);
                field.schedule.Execute(() =>
                {
                    field.Focus();
                    field.SelectAll();
                });
            });
            titleHost.Add(title);
            header.Add(titleHost);

            var controls = new VisualElement();
            controls.AddToClassList("kanban-detail-header-controls");

            var statusIndex = Array.FindIndex(StatusChoices, c => c.Status == _card.Status);
            var status = new DropdownField(
                StatusChoices.Select(c => c.Label).ToList(), Mathf.Max(statusIndex, 0));
            status.AddToClassList("styled-dropdown");
            status.RegisterValueChangedCallback(_ =>
            {
                var chosen = StatusChoices[status.index].Status;
                if (chosen == _card.Status)
                    return;
                Store.ReorderCard(_card, chosen, int.MaxValue);
                NotifyChanged();
                RenderFeed();
            });
            controls.Add(status);

            var priority = new DropdownField(
                new List<string> { "LOW", "MEDIUM", "HIGH", "CRITICAL" }, (int)_card.Priority);
            priority.AddToClassList("styled-dropdown");
            priority.RegisterValueChangedCallback(_ =>
            {
                _card.Priority = (TaskPriority)priority.index;
                NotifyChanged();
            });
            controls.Add(priority);

            var labels = new Button(ShowLabelMenu) { text = "LABELS" };
            labels.AddToClassList("btn-secondary");
            labels.AddToClassList("btn-compact");
            controls.Add(labels);

            var close = new Button(Close) { text = "✕", tooltip = "Close (Esc)" };
            close.AddToClassList("btn-tertiary");
            controls.Add(close);

            header.Add(controls);
            return header;
        }

        // ── Labels ───────────────────────────────────────────────────────

        private void RenderLabels()
        {
            _labelsRow.Clear();
            foreach (var id in _card.LabelIds.ToList())
            {
                var label = Store.FindLabel(id);
                if (label == null)
                {
                    _card.LabelIds.Remove(id);
                    continue;
                }

                var pill = new Label(label.Name) { tooltip = "Click to remove" };
                pill.AddToClassList("kanban-detail-label-pill");
                pill.style.backgroundColor = new Color(label.Color.r, label.Color.g, label.Color.b, 0.25f);
                pill.style.color = label.Color;
                pill.style.borderTopColor = pill.style.borderBottomColor =
                    pill.style.borderLeftColor = pill.style.borderRightColor = label.Color;
                pill.RegisterCallback<ClickEvent>(_ =>
                {
                    _card.LabelIds.Remove(label.Id);
                    NotifyChanged();
                    RenderLabels();
                });
                _labelsRow.Add(pill);
            }
        }

        private void ShowLabelMenu()
        {
            var menu = new GenericDropdownMenu();
            foreach (var label in Store.Labels)
            {
                var id = label.Id;
                var isSet = _card.LabelIds.Contains(id);
                menu.AddItem(label.Name, isSet, () =>
                {
                    if (isSet)
                        _card.LabelIds.Remove(id);
                    else
                        _card.LabelIds.Add(id);
                    NotifyChanged();
                    RenderLabels();
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem("Edit labels…", false, ShowLabelEditor);
            menu.DropDown(_panel.worldBound, _panel, anchored: false);
        }

        private void ShowLabelEditor()
        {
            var editor = new VisualElement();
            editor.AddToClassList("kanban-label-editor");

            var list = new VisualElement();

            void RenderList()
            {
                list.Clear();
                foreach (var label in Store.Labels.ToList())
                {
                    var row = new VisualElement();
                    row.AddToClassList("kanban-label-editor-row");

                    var swatch = new VisualElement();
                    swatch.AddToClassList("kanban-label-editor-swatch");
                    swatch.style.backgroundColor = label.Color;
                    row.Add(swatch);

                    var name = new Label(label.Name);
                    name.AddToClassList("kanban-label-editor-name");
                    row.Add(name);

                    var remove = new Button(() =>
                    {
                        Store.RemoveLabel(label.Id);
                        NotifyChanged();
                        RenderLabels();
                        RenderList();
                    }) { text = "✕", tooltip = "Delete label (removed from all cards)" };
                    remove.AddToClassList("btn-tertiary");
                    row.Add(remove);

                    list.Add(row);
                }
            }

            RenderList();
            editor.Add(list);

            var composer = new VisualElement();
            composer.AddToClassList("kanban-label-editor-composer");

            var nameField = new TextField { tooltip = "New label name" };
            nameField.AddToClassList("styled-text-field");
            nameField.AddToClassList("kanban-label-editor-field");
            composer.Add(nameField);

            var picked = LabelPalette[0];
            var swatches = new VisualElement();
            swatches.AddToClassList("kanban-label-editor-swatches");
            var selectable = new List<VisualElement>();
            foreach (var color in LabelPalette)
            {
                var c = color;
                var swatch = new VisualElement();
                swatch.AddToClassList("kanban-label-editor-swatch");
                swatch.style.backgroundColor = c;
                swatch.RegisterCallback<ClickEvent>(_ =>
                {
                    picked = c;
                    foreach (var s in selectable)
                        s.RemoveFromClassList("kanban-label-editor-swatch-selected");
                    swatch.AddToClassList("kanban-label-editor-swatch-selected");
                });
                selectable.Add(swatch);
                swatches.Add(swatch);
            }
            selectable[0].AddToClassList("kanban-label-editor-swatch-selected");
            composer.Add(swatches);

            var add = new Button(() =>
            {
                var name = nameField.value?.Trim();
                if (string.IsNullOrEmpty(name))
                    return;
                Store.AddLabel(name!.ToUpperInvariant(), picked);
                nameField.SetValueWithoutNotify(string.Empty);
                RenderList();
            }) { text = "ADD" };
            add.AddToClassList("btn-primary");
            add.AddToClassList("btn-compact");
            composer.Add(add);

            editor.Add(composer);

            var closeRow = new VisualElement();
            closeRow.AddToClassList("kanban-label-editor-close-row");
            var done = new Button(() => editor.RemoveFromHierarchy()) { text = "DONE" };
            done.AddToClassList("btn-secondary");
            done.AddToClassList("btn-compact");
            closeRow.Add(done);
            editor.Add(closeRow);

            // Insert right under the labels row so it reads as an inline popover.
            _panel.Insert(_panel.IndexOf(_labelsRow) + 1, editor);
        }

        // ── Dates ────────────────────────────────────────────────────────

        private VisualElement BuildDatesSection()
        {
            var section = new VisualElement();
            section.Add(SectionTitle("DATES"));

            section.Add(BuildDateRow("START", () => _card.StartTicksUtc, v =>
            {
                _card.StartTicksUtc = v;
                NotifyChanged();
            }, logDue: false));

            var dueRow = BuildDateRow("DUE", () => _card.DueTicksUtc, v =>
            {
                _card.DueTicksUtc = v;
                Store.LogActivity(_card, "due-set",
                    v == 0 ? "cleared" : FormatDate(v));
                NotifyChanged();
                RenderFeed();
            }, logDue: true);

            var complete = new Toggle("COMPLETE") { value = _card.DueComplete };
            complete.AddToClassList("kanban-detail-due-complete");
            complete.RegisterValueChangedCallback(evt =>
            {
                _card.DueComplete = evt.newValue;
                NotifyChanged();
            });
            dueRow.Add(complete);
            section.Add(dueRow);

            return section;
        }

        private VisualElement BuildDateRow(string label, Func<long> get, Action<long> set, bool logDue)
        {
            var row = new VisualElement();
            row.AddToClassList("kanban-detail-date-row");

            var caption = new Label(label);
            caption.AddToClassList("kanban-detail-date-caption");
            row.Add(caption);

            var field = new TextField { tooltip = "yyyy-MM-dd HH:mm (time optional)" };
            field.AddToClassList("styled-text-field");
            field.AddToClassList("kanban-detail-date-field");
            field.SetValueWithoutNotify(get() == 0 ? string.Empty : FormatDate(get()));
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                var text = field.value?.Trim();
                field.RemoveFromClassList("kanban-detail-date-invalid");
                if (string.IsNullOrEmpty(text))
                {
                    if (get() != 0)
                        set(0);
                    return;
                }

                if (TryParseDate(text!, out var ticks))
                {
                    if (ticks != get())
                        set(ticks);
                    field.SetValueWithoutNotify(FormatDate(ticks));
                }
                else
                {
                    field.AddToClassList("kanban-detail-date-invalid");
                }
            });
            row.Add(field);

            void Quick(string text, Func<long> compute)
            {
                var button = new Button(() =>
                {
                    var ticks = compute();
                    set(ticks);
                    field.SetValueWithoutNotify(ticks == 0 ? string.Empty : FormatDate(ticks));
                    field.RemoveFromClassList("kanban-detail-date-invalid");
                }) { text = text };
                button.AddToClassList("btn-tertiary");
                button.AddToClassList("btn-compact");
                row.Add(button);
            }

            Quick("TODAY", () => LocalEndOfDayUtcTicks(DateTime.Now));
            Quick("+1D", () => LocalEndOfDayUtcTicks(DateTime.Now.AddDays(1)));
            Quick("+1W", () => LocalEndOfDayUtcTicks(DateTime.Now.AddDays(7)));
            Quick("CLR", () => 0);

            return row;
        }

        private static long LocalEndOfDayUtcTicks(DateTime local)
            => local.Date.AddHours(18).ToUniversalTime().Ticks;

        private static bool TryParseDate(string text, out long utcTicks)
        {
            utcTicks = 0;
            string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var parsed))
                return false;
            utcTicks = parsed.ToUniversalTime().Ticks;
            return true;
        }

        private static string FormatDate(long utcTicks)
            => new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        // ── Cover ────────────────────────────────────────────────────────

        private VisualElement BuildCoverSection()
        {
            var section = new VisualElement();
            section.Add(SectionTitle("COVER"));

            var row = new VisualElement();
            row.AddToClassList("kanban-cover-row");

            var swatches = new List<VisualElement>();

            void SyncSelection()
            {
                for (var i = 0; i < LabelPalette.Length; i++)
                    swatches[i].EnableInClassList("kanban-label-editor-swatch-selected",
                        _card.HasCover && _card.CoverColor == LabelPalette[i]);
            }

            foreach (var color in LabelPalette)
            {
                var c = color;
                var swatch = new VisualElement();
                swatch.AddToClassList("kanban-cover-swatch");
                swatch.style.backgroundColor = c;
                swatch.RegisterCallback<ClickEvent>(_ =>
                {
                    _card.HasCover = true;
                    _card.CoverColor = c;
                    NotifyChanged();
                    SyncSelection();
                });
                swatches.Add(swatch);
                row.Add(swatch);
            }

            var none = new Button(() =>
            {
                _card.HasCover = false;
                NotifyChanged();
                SyncSelection();
            }) { text = "NONE" };
            none.AddToClassList("btn-tertiary");
            none.AddToClassList("btn-compact");
            row.Add(none);

            SyncSelection();
            section.Add(row);
            return section;
        }

        // ── Attachments ──────────────────────────────────────────────────

        private VisualElement BuildAttachmentsSection()
        {
            var section = new VisualElement();
            section.Add(SectionTitleRow("ATTACHMENTS", "FILE…", AddFileAttachment));

            _attachmentsHost = new VisualElement();
            section.Add(_attachmentsHost);
            RenderAttachments();

            var composer = new VisualElement();
            composer.AddToClassList("kanban-detail-comment-composer");

            var urlField = new TextField { tooltip = "https://… — Enter or ADD" };
            urlField.AddToClassList("styled-text-field");
            urlField.AddToClassList("kanban-detail-comment-field");

            void AddUrl()
            {
                var url = urlField.value?.Trim();
                if (string.IsNullOrEmpty(url))
                    return;
                AddAttachment(url!, isUrl: true);
                urlField.SetValueWithoutNotify(string.Empty);
            }

            urlField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    AddUrl();
                    evt.StopPropagation();
                }
            });
            composer.Add(urlField);

            var add = new Button(AddUrl) { text = "ADD URL" };
            add.AddToClassList("btn-secondary");
            add.AddToClassList("btn-compact");
            composer.Add(add);

            section.Add(composer);
            return section;
        }

        private void AddFileAttachment()
        {
            var path = EditorUtility.OpenFilePanel("Attach file", string.Empty, string.Empty);
            if (!string.IsNullOrEmpty(path))
                AddAttachment(path, isUrl: false);
        }

        private void AddAttachment(string path, bool isUrl)
        {
            _card.Attachments.Add(new MatrixSpaceTaskStore.Attachment
            {
                Id = Guid.NewGuid().ToString("N"),
                Path = path,
                IsUrl = isUrl,
            });
            Store.LogActivity(_card, "attached", Truncate(path, 60));
            NotifyChanged();
            RenderAttachments();
            RenderFeed();
        }

        private void RenderAttachments()
        {
            _attachmentsHost.Clear();
            foreach (var attachment in _card.Attachments.ToList())
            {
                var row = new VisualElement();
                row.AddToClassList("kanban-attachment-row");

                var icon = new Label(attachment.IsUrl ? "🔗" : "📄");
                icon.AddToClassList("kanban-attachment-icon");
                row.Add(icon);

                var display = attachment.IsUrl ? attachment.Path : Path.GetFileName(attachment.Path);
                var name = new Label(display) { tooltip = attachment.Path };
                name.AddToClassList("kanban-attachment-name");
                row.Add(name);

                var open = new Button(() =>
                {
                    if (attachment.IsUrl)
                        Application.OpenURL(attachment.Path);
                    else
                        EditorUtility.RevealInFinder(attachment.Path);
                }) { text = "OPEN" };
                open.AddToClassList("btn-secondary");
                open.AddToClassList("btn-compact");
                row.Add(open);

                var remove = new Button(() =>
                {
                    _card.Attachments.Remove(attachment);
                    Store.LogActivity(_card, "attachment-removed", Truncate(display, 60));
                    NotifyChanged();
                    RenderAttachments();
                    RenderFeed();
                }) { text = "✕" };
                remove.AddToClassList("btn-tertiary");
                row.Add(remove);

                _attachmentsHost.Add(row);
            }
        }

        // ── Custom fields ────────────────────────────────────────────────

        private void RenderFields()
        {
            _fieldsHost.Clear();

            if (Store.CustomFields.Count == 0)
            {
                var empty = new Label("No custom fields — MANAGE… to define some.");
                empty.AddToClassList("kanban-feed-meta");
                _fieldsHost.Add(empty);
                return;
            }

            foreach (var definition in Store.CustomFields.ToList())
            {
                var row = new VisualElement();
                row.AddToClassList("kanban-field-row");

                var caption = new Label(definition.Name.ToUpperInvariant());
                caption.AddToClassList("kanban-field-caption");
                row.Add(caption);

                row.Add(BuildFieldEditor(definition));
                _fieldsHost.Add(row);
            }
        }

        private VisualElement BuildFieldEditor(MatrixSpaceTaskStore.CustomFieldDefinition definition)
        {
            var value = MatrixSpaceTaskStore.GetFieldValue(_card, definition.Id);

            void Commit(string newValue)
            {
                MatrixSpaceTaskStore.SetFieldValue(_card, definition.Id, newValue);
                NotifyChanged();
            }

            switch (definition.Type)
            {
                case CustomFieldType.Checkbox:
                {
                    var toggle = new Toggle { value = value == "1" };
                    toggle.RegisterValueChangedCallback(evt => Commit(evt.newValue ? "1" : string.Empty));
                    return toggle;
                }

                case CustomFieldType.Dropdown:
                {
                    var choices = new List<string> { "—" };
                    choices.AddRange(definition.Options);
                    var index = Mathf.Max(0, choices.IndexOf(value));
                    var dropdown = new DropdownField(choices, index);
                    dropdown.AddToClassList("styled-dropdown");
                    dropdown.RegisterValueChangedCallback(_ =>
                        Commit(dropdown.index <= 0 ? string.Empty : choices[dropdown.index]));
                    return dropdown;
                }

                case CustomFieldType.Number:
                {
                    var field = new TextField { value = value };
                    field.AddToClassList("styled-text-field");
                    field.AddToClassList("kanban-field-input");
                    field.RegisterCallback<FocusOutEvent>(_ =>
                    {
                        var text = field.value?.Trim() ?? string.Empty;
                        field.RemoveFromClassList("kanban-detail-date-invalid");
                        if (string.IsNullOrEmpty(text))
                        {
                            Commit(string.Empty);
                            return;
                        }
                        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        {
                            var normalized = number.ToString(CultureInfo.InvariantCulture);
                            field.SetValueWithoutNotify(normalized);
                            Commit(normalized);
                        }
                        else
                        {
                            field.AddToClassList("kanban-detail-date-invalid");
                        }
                    });
                    return field;
                }

                case CustomFieldType.Date:
                {
                    var field = new TextField
                    {
                        value = long.TryParse(value, out var ticks) && ticks > 0
                            ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd")
                            : string.Empty,
                        tooltip = "yyyy-MM-dd",
                    };
                    field.AddToClassList("styled-text-field");
                    field.AddToClassList("kanban-field-input");
                    field.RegisterCallback<FocusOutEvent>(_ =>
                    {
                        var text = field.value?.Trim() ?? string.Empty;
                        field.RemoveFromClassList("kanban-detail-date-invalid");
                        if (string.IsNullOrEmpty(text))
                        {
                            Commit(string.Empty);
                            return;
                        }
                        if (TryParseDate(text, out var parsedTicks))
                        {
                            Commit(parsedTicks.ToString());
                            field.SetValueWithoutNotify(
                                new DateTime(parsedTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd"));
                        }
                        else
                        {
                            field.AddToClassList("kanban-detail-date-invalid");
                        }
                    });
                    return field;
                }

                default: // Text
                {
                    var field = new TextField { value = value };
                    field.AddToClassList("styled-text-field");
                    field.AddToClassList("kanban-field-input");
                    field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value?.Trim() ?? string.Empty));
                    return field;
                }
            }
        }

        private void ShowFieldManager()
        {
            var editor = new VisualElement();
            editor.AddToClassList("kanban-label-editor");

            var list = new VisualElement();

            void RenderList()
            {
                list.Clear();
                foreach (var definition in Store.CustomFields.ToList())
                {
                    var row = new VisualElement();
                    row.AddToClassList("kanban-label-editor-row");

                    var name = new Label($"{definition.Name}  ·  {definition.Type}");
                    name.AddToClassList("kanban-label-editor-name");
                    row.Add(name);

                    var remove = new Button(() =>
                    {
                        Store.RemoveCustomField(definition.Id);
                        NotifyChanged();
                        RenderList();
                        RenderFields();
                    }) { text = "✕", tooltip = "Delete field (values removed from all cards)" };
                    remove.AddToClassList("btn-tertiary");
                    row.Add(remove);

                    list.Add(row);
                }
            }

            RenderList();
            editor.Add(list);

            var composer = new VisualElement();
            composer.AddToClassList("kanban-label-editor-composer");

            var nameField = new TextField { tooltip = "New field name" };
            nameField.AddToClassList("styled-text-field");
            nameField.AddToClassList("kanban-label-editor-field");
            composer.Add(nameField);

            var typeNames = Enum.GetNames(typeof(CustomFieldType)).ToList();
            var typeField = new DropdownField(typeNames, 0);
            typeField.AddToClassList("styled-dropdown");
            composer.Add(typeField);

            var optionsField = new TextField
            {
                tooltip = "Dropdown options, comma-separated",
                style = { display = DisplayStyle.None },
            };
            optionsField.AddToClassList("styled-text-field");
            optionsField.AddToClassList("kanban-label-editor-field");
            typeField.RegisterValueChangedCallback(_ =>
                optionsField.style.display = (CustomFieldType)typeField.index == CustomFieldType.Dropdown
                    ? DisplayStyle.Flex : DisplayStyle.None);
            composer.Add(optionsField);

            var add = new Button(() =>
            {
                var name = nameField.value?.Trim();
                if (string.IsNullOrEmpty(name))
                    return;
                var type = (CustomFieldType)typeField.index;
                var options = type == CustomFieldType.Dropdown
                    ? (optionsField.value ?? string.Empty)
                        .Split(',')
                        .Select(o => o.Trim())
                        .Where(o => o.Length > 0)
                        .ToList()
                    : new List<string>();
                Store.AddCustomField(name!, type, options);
                nameField.SetValueWithoutNotify(string.Empty);
                optionsField.SetValueWithoutNotify(string.Empty);
                RenderList();
                RenderFields();
            }) { text = "ADD" };
            add.AddToClassList("btn-primary");
            add.AddToClassList("btn-compact");
            composer.Add(add);

            editor.Add(composer);

            var closeRow = new VisualElement();
            closeRow.AddToClassList("kanban-label-editor-close-row");
            var done = new Button(() => editor.RemoveFromHierarchy()) { text = "DONE" };
            done.AddToClassList("btn-secondary");
            done.AddToClassList("btn-compact");
            closeRow.Add(done);
            editor.Add(closeRow);

            _fieldsHost.Insert(0, editor);
        }

        // ── Description ──────────────────────────────────────────────────

        private VisualElement BuildDescriptionSection()
        {
            var section = new VisualElement();
            section.Add(SectionTitle("DESCRIPTION"));

            var host = new VisualElement();
            section.Add(host);

            void RenderView()
            {
                host.Clear();
                var hasText = !string.IsNullOrEmpty(_card.Description);
                VisualElement view;
                if (hasText)
                {
                    view = RenderMarkdown(_card.Description);
                }
                else
                {
                    var empty = new Label("Click to add a description…");
                    empty.AddToClassList("kanban-detail-description-empty");
                    view = empty;
                }
                view.AddToClassList("kanban-detail-description");
                view.tooltip = "Click to edit";
                view.RegisterCallback<ClickEvent>(_ => RenderEditor());
                host.Add(view);
            }

            void RenderEditor()
            {
                host.Clear();
                var field = new TextField { multiline = true, value = _card.Description };
                field.AddToClassList("styled-text-field");
                field.AddToClassList("kanban-detail-description-field");

                void Commit()
                {
                    var value = field.value ?? string.Empty;
                    if (value != _card.Description)
                    {
                        _card.Description = value;
                        NotifyChanged();
                    }
                    RenderView();
                }

                field.RegisterCallback<FocusOutEvent>(_ => Commit());
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter && evt.ctrlKey)
                    {
                        Commit();
                        evt.StopPropagation();
                    }
                });
                host.Add(field);
                field.schedule.Execute(() => field.Focus());
            }

            RenderView();
            return section;
        }

        /// <summary>
        /// Minimal markdown renderer for the description view mode: # / ## headings,
        /// - / * bullets, and inline **bold** / *italic* / `code` via TextElement rich text.
        /// </summary>
        private static VisualElement RenderMarkdown(string text)
        {
            var container = new VisualElement();
            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.Length == 0)
                {
                    var spacer = new VisualElement { style = { height = 4 } };
                    container.Add(spacer);
                    continue;
                }

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    container.Add(MarkdownLabel(line[3..], "kanban-md-h2"));
                }
                else if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    container.Add(MarkdownLabel(line[2..], "kanban-md-h1"));
                }
                else if (line.StartsWith("- ", StringComparison.Ordinal) ||
                         line.StartsWith("* ", StringComparison.Ordinal))
                {
                    var row = new VisualElement();
                    row.AddToClassList("kanban-md-li");
                    var bullet = new Label("▸");
                    bullet.AddToClassList("kanban-md-bullet");
                    row.Add(bullet);
                    var content = MarkdownLabel(line[2..], null);
                    content.style.flexShrink = 1;
                    row.Add(content);
                    container.Add(row);
                }
                else
                {
                    container.Add(MarkdownLabel(line, null));
                }
            }
            return container;

            static Label MarkdownLabel(string content, string? className)
            {
                var label = new Label(MarkdownInline(content)) { enableRichText = true };
                label.AddToClassList("kanban-md-text");
                if (className != null)
                    label.AddToClassList(className);
                return label;
            }
        }

        private static string MarkdownInline(string text)
        {
            // A zero-width space after '<' stops user-typed text from parsing as a rich-text
            // tag, without visibly altering the content; markdown tags are added afterwards.
            text = text.Replace("<", "<\u200B");
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            text = Regex.Replace(text, @"\*(.+?)\*", "<i>$1</i>");
            text = Regex.Replace(text, @"`(.+?)`", "<color=#3CFF64>$1</color>");
            return text;
        }

        // ── Checklists ───────────────────────────────────────────────────

        private void AddChecklist()
        {
            _card.Checklists.Add(new MatrixSpaceTaskStore.Checklist
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "CHECKLIST",
            });
            NotifyChanged();
            RenderChecklists();
        }

        private void RenderChecklists()
        {
            _checklistsHost.Clear();
            foreach (var checklist in _card.Checklists.ToList())
                _checklistsHost.Add(BuildChecklist(checklist));
        }

        private VisualElement BuildChecklist(MatrixSpaceTaskStore.Checklist checklist)
        {
            var root = new VisualElement();
            root.AddToClassList("kanban-checklist");

            var header = new VisualElement();
            header.AddToClassList("kanban-checklist-header");

            var title = new Label(checklist.Title) { tooltip = "Click to rename" };
            title.AddToClassList("kanban-checklist-title");
            title.RegisterCallback<ClickEvent>(_ =>
            {
                var field = new TextField { value = checklist.Title };
                field.AddToClassList("styled-text-field");
                void Commit()
                {
                    var value = field.value?.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        checklist.Title = value!.ToUpperInvariant();
                        NotifyChanged();
                    }
                    RenderChecklists();
                }
                field.RegisterCallback<FocusOutEvent>(_ => Commit());
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                        Commit();
                });
                var index = header.IndexOf(title);
                header.Remove(title);
                header.Insert(index, field);
                field.schedule.Execute(() => field.Focus());
            });
            header.Add(title);

            var done = checklist.Items.Count(i => i.Done);
            var counter = new Label($"{done}/{checklist.Items.Count}");
            counter.AddToClassList("kanban-checklist-counter");
            header.Add(counter);

            var remove = new Button(() =>
            {
                _card.Checklists.Remove(checklist);
                NotifyChanged();
                RenderChecklists();
            }) { text = "✕", tooltip = "Delete checklist" };
            remove.AddToClassList("btn-tertiary");
            header.Add(remove);

            root.Add(header);

            var track = new VisualElement();
            track.AddToClassList("kanban-progress-track");
            var fill = new VisualElement();
            fill.AddToClassList("kanban-progress-fill");
            fill.style.width = Length.Percent(checklist.Items.Count == 0
                ? 0f : done * 100f / checklist.Items.Count);
            track.Add(fill);
            root.Add(track);

            foreach (var item in checklist.Items.ToList())
            {
                var row = new VisualElement();
                row.AddToClassList("kanban-checklist-item");

                var toggle = new Toggle { value = item.Done };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    item.Done = evt.newValue;
                    NotifyChanged();
                    RenderChecklists();
                });
                row.Add(toggle);

                var text = new Label(item.Text);
                text.AddToClassList("kanban-checklist-item-text");
                if (item.Done)
                    text.AddToClassList("kanban-checklist-item-done");
                row.Add(text);

                var removeItem = new Button(() =>
                {
                    checklist.Items.Remove(item);
                    NotifyChanged();
                    RenderChecklists();
                }) { text = "✕" };
                removeItem.AddToClassList("btn-tertiary");
                row.Add(removeItem);

                root.Add(row);
            }

            var composer = new VisualElement();
            composer.AddToClassList("kanban-checklist-composer");

            var itemField = new TextField { tooltip = "New item — Enter to add" };
            itemField.AddToClassList("styled-text-field");
            itemField.AddToClassList("kanban-checklist-item-field");

            void AddItem()
            {
                var value = itemField.value?.Trim();
                if (string.IsNullOrEmpty(value))
                    return;
                checklist.Items.Add(new MatrixSpaceTaskStore.ChecklistItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = value!,
                });
                NotifyChanged();
                RenderChecklists();
            }

            itemField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    AddItem();
                    evt.StopPropagation();
                }
            });
            composer.Add(itemField);

            var add = new Button(AddItem) { text = "ADD" };
            add.AddToClassList("btn-secondary");
            add.AddToClassList("btn-compact");
            composer.Add(add);

            root.Add(composer);
            return root;
        }

        // ── Assignee ─────────────────────────────────────────────────────

        private VisualElement BuildAssigneeSection()
        {
            var section = new VisualElement();
            section.Add(SectionTitle("ASSIGNEE"));

            var targets = _paneTargets?.Invoke() ?? new List<(string, string, bool)>();
            var choices = new List<string> { "Unassigned" };
            choices.AddRange(targets.Select(t => t.Label));

            var index = 0;
            if (!string.IsNullOrEmpty(_card.AssignedPaneId))
            {
                var found = targets.FindIndex(t => t.PaneId == _card.AssignedPaneId);
                index = found >= 0 ? found + 1 : 0;
            }

            var dropdown = new DropdownField(choices, index);
            dropdown.AddToClassList("styled-dropdown");
            dropdown.RegisterValueChangedCallback(_ =>
            {
                _card.AssignedPaneId = dropdown.index <= 0 ? null : targets[dropdown.index - 1].PaneId;
                NotifyChanged();
            });
            section.Add(dropdown);
            return section;
        }

        // ── Comments & activity ──────────────────────────────────────────

        private VisualElement BuildCommentComposer()
        {
            var composer = new VisualElement();
            composer.AddToClassList("kanban-detail-comment-composer");

            var field = new TextField { multiline = true, tooltip = "Write a comment…" };
            field.AddToClassList("styled-text-field");
            field.AddToClassList("kanban-detail-comment-field");
            composer.Add(field);

            var post = new Button(() =>
            {
                var text = field.value?.Trim();
                if (string.IsNullOrEmpty(text))
                    return;
                _card.Comments.Add(new MatrixSpaceTaskStore.CardComment
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TicksUtc = DateTime.UtcNow.Ticks,
                    Author = "you",
                    Text = text!,
                });
                Store.LogActivity(_card, "commented", Truncate(text!, 60));
                field.SetValueWithoutNotify(string.Empty);
                NotifyChanged();
                RenderFeed();
            }) { text = "POST" };
            post.AddToClassList("btn-primary");
            post.AddToClassList("btn-compact");
            composer.Add(post);

            return composer;
        }

        private void RenderFeed()
        {
            _feedHost.Clear();

            var entries = new List<(long Ticks, VisualElement Row)>();

            foreach (var comment in _card.Comments)
            {
                var row = new VisualElement();
                row.AddToClassList("kanban-feed-comment");

                var meta = new Label($"{comment.Author} · {FormatFeedTime(comment.TicksUtc)}");
                meta.AddToClassList("kanban-feed-meta");
                row.Add(meta);

                var text = new Label(comment.Text);
                text.AddToClassList("kanban-feed-text");
                row.Add(text);

                entries.Add((comment.TicksUtc, row));
            }

            foreach (var activity in _card.Activity)
            {
                var row = new VisualElement();
                row.AddToClassList("kanban-feed-activity");

                var text = new Label(
                    $"{FormatFeedTime(activity.TicksUtc)}  {activity.Kind.ToUpperInvariant()}  {activity.Detail}");
                text.AddToClassList("kanban-feed-meta");
                row.Add(text);

                entries.Add((activity.TicksUtc, row));
            }

            foreach (var (_, row) in entries.OrderByDescending(e => e.Ticks))
                _feedHost.Add(row);

            if (entries.Count == 0)
            {
                var empty = new Label("No activity yet.");
                empty.AddToClassList("kanban-feed-meta");
                _feedHost.Add(empty);
            }
        }

        private static string FormatFeedTime(long utcTicks)
            => new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm dd MMM");

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text[..max] + "…";

        // ── Small helpers ────────────────────────────────────────────────

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("kanban-detail-section-title");
            return label;
        }

        private static VisualElement SectionTitleRow(string text, string buttonText, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("kanban-detail-section-row");
            row.Add(SectionTitle(text));
            var spacer = new VisualElement();
            spacer.AddToClassList("kanban-header-spacer");
            row.Add(spacer);
            var button = new Button(onClick) { text = buttonText };
            button.AddToClassList("btn-secondary");
            button.AddToClassList("btn-compact");
            row.Add(button);
            return row;
        }
    }
}
