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
    /// Force-directed knowledge graph of memory nodes. Edges + circles drawn via painter2D;
    /// labels are pooled child Labels (painter2D has no text). The simulation runs a bounded
    /// number of iterations on EditorApplication.update while the view is attached.
    /// </summary>
    public sealed class MemoryGraphView : VisualElement
    {
        private sealed class SimNode
        {
            public MemoryGraphStore.MemoryNode Data = null!;
            public Vector2 Position;
            public Vector2 Force;
            public float Radius;
            public Color Color;
            public Label Label = null!;
        }

        public event Action<string>? NodeSelected;

        private static readonly Color EdgeColor = new(0.35f, 0.67f, 0.43f, 0.35f);
        private static readonly Color SelectedRing = new(0.24f, 1f, 0.39f, 1f);

        // Section palette: Matrix-green variants (hash section name → slot).
        private static readonly Color[] SectionColors =
        {
            new(0.24f, 1.00f, 0.39f, 0.9f),
            new(0.55f, 0.95f, 0.55f, 0.9f),
            new(0.20f, 0.75f, 0.55f, 0.9f),
            new(0.70f, 1.00f, 0.45f, 0.9f),
            new(0.35f, 0.85f, 0.75f, 0.9f),
        };

        private readonly List<SimNode> _nodes = new();
        private readonly Dictionary<string, SimNode> _byKey = new();
        private readonly List<(SimNode A, SimNode B)> _edges = new();

        private string? _selectedKey;
        private int _iterationsLeft;
        private bool _ticking;

        public MemoryGraphView()
        {
            AddToClassList("memory-graph");
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(_ => StartTicking());
            RegisterCallback<DetachFromPanelEvent>(_ => StopTicking());
            RegisterCallback<GeometryChangedEvent>(_ => WarmUp(60));
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        public void SetData(MemoryGraphStore store, string? selectedKey)
        {
            _selectedKey = selectedKey;

            // Keep positions of surviving nodes so the graph stays stable across rescans.
            var oldPositions = new Dictionary<string, Vector2>();
            foreach (var node in _nodes)
                oldPositions[node.Data.Key] = node.Position;

            foreach (var node in _nodes)
                node.Label.RemoveFromHierarchy();
            _nodes.Clear();
            _byKey.Clear();
            _edges.Clear();

            var rect = contentRect;
            var center = rect.width > 1 ? rect.center : new Vector2(300, 200);
            var rng = new System.Random(12345);

            foreach (var data in store.Nodes)
            {
                var links = data.OutgoingLinks.Count + data.Backlinks.Count;
                var sim = new SimNode
                {
                    Data = data,
                    Radius = data.IsHub ? 12f : Mathf.Clamp(6f + links * 1.2f, 6f, 12f),
                    Color = SectionColors[Math.Abs(data.Section.GetHashCode()) % SectionColors.Length],
                    Position = oldPositions.TryGetValue(data.Key, out var p)
                        ? p
                        : center + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 220f,
                };

                var label = new Label(data.Title);
                label.AddToClassList("memory-graph-label");
                label.pickingMode = PickingMode.Ignore;
                Add(label);
                sim.Label = label;

                _nodes.Add(sim);
                _byKey[data.Key] = sim;
            }

            foreach (var node in _nodes)
            {
                foreach (var link in node.Data.OutgoingLinks)
                {
                    if (_byKey.TryGetValue(link, out var target))
                        _edges.Add((node, target));
                }
            }

            WarmUp(300);
        }

        public void SetSelected(string? key)
        {
            _selectedKey = key;
            MarkDirtyRepaint();
        }

        private void WarmUp(int iterations)
        {
            _iterationsLeft = Math.Max(_iterationsLeft, iterations);
            StartTicking();
        }

        private void StartTicking()
        {
            if (_ticking || panel == null)
                return;
            _ticking = true;
            EditorApplication.update += Tick;
        }

        private void StopTicking()
        {
            if (!_ticking)
                return;
            _ticking = false;
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (_iterationsLeft <= 0 || _nodes.Count == 0)
            {
                StopTicking();
                return;
            }

            _iterationsLeft--;
            StepSimulation();
            SyncLabels();
            MarkDirtyRepaint();
        }

        private void StepSimulation()
        {
            var rect = contentRect;
            if (rect.width < 10f || rect.height < 10f)
                return;

            var center = rect.center;
            const float repulsion = 9000f;
            const float springLength = 110f;
            const float springK = 0.02f;
            const float centering = 0.005f;
            const float damping = 0.85f;

            foreach (var node in _nodes)
                node.Force = (center - node.Position) * centering * (node.Data.IsHub ? 4f : 1f);

            for (var i = 0; i < _nodes.Count; i++)
            {
                for (var j = i + 1; j < _nodes.Count; j++)
                {
                    var a = _nodes[i];
                    var b = _nodes[j];
                    var delta = a.Position - b.Position;
                    var distSq = Mathf.Max(delta.sqrMagnitude, 25f);
                    var push = delta.normalized * (repulsion / distSq);
                    a.Force += push;
                    b.Force -= push;
                }
            }

            foreach (var (a, b) in _edges)
            {
                var delta = b.Position - a.Position;
                var dist = Mathf.Max(delta.magnitude, 1f);
                var pull = delta.normalized * ((dist - springLength) * springK);
                a.Force += pull;
                b.Force -= pull;
            }

            var margin = 24f;
            foreach (var node in _nodes)
            {
                node.Position += node.Force * damping;
                node.Position.x = Mathf.Clamp(node.Position.x, margin, rect.width - margin);
                node.Position.y = Mathf.Clamp(node.Position.y, margin, rect.height - margin);
            }
        }

        private void SyncLabels()
        {
            foreach (var node in _nodes)
            {
                node.Label.style.left = node.Position.x - 40;
                node.Label.style.top = node.Position.y + node.Radius + 2;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;

            painter.strokeColor = EdgeColor;
            painter.lineWidth = 1f;
            foreach (var (a, b) in _edges)
            {
                painter.BeginPath();
                painter.MoveTo(a.Position);
                painter.LineTo(b.Position);
                painter.Stroke();
            }

            foreach (var node in _nodes)
            {
                if (node.Data.Key == _selectedKey)
                {
                    painter.strokeColor = SelectedRing;
                    painter.lineWidth = 2f;
                    painter.BeginPath();
                    painter.Arc(node.Position, node.Radius + 4f, 0, 360);
                    painter.Stroke();
                }

                painter.fillColor = node.Data.IsOrphan
                    ? new Color(node.Color.r, node.Color.g, node.Color.b, 0.35f)
                    : node.Color;
                painter.BeginPath();
                painter.Arc(node.Position, node.Radius, 0, 360);
                painter.Fill();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var local = new Vector2(evt.localPosition.x, evt.localPosition.y);
            SimNode? best = null;
            var bestDist = float.MaxValue;
            foreach (var node in _nodes)
            {
                var dist = Vector2.Distance(local, node.Position);
                if (dist < node.Radius + 8f && dist < bestDist)
                {
                    best = node;
                    bestDist = dist;
                }
            }

            if (best != null)
            {
                _selectedKey = best.Data.Key;
                MarkDirtyRepaint();
                NodeSelected?.Invoke(best.Data.Key);
            }
        }
    }
}
