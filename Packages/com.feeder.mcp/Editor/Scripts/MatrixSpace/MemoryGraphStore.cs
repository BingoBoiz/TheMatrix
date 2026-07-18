#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// MatrixMemory data layer: MatrixSpace/memory/*.md files with a minimal frontmatter
    /// (title / section / hub) and [[wiki-links]] in the body. The filesystem is the source
    /// of truth — Scan() rebuilds nodes, links, backlinks and orphan stats on demand.
    /// </summary>
    public sealed class MemoryGraphStore
    {
        public sealed class MemoryNode
        {
            public string FileName = string.Empty;   // e.g. "auth-flow.md"
            public string Key = string.Empty;        // file name without extension, lowercase
            public string Title = string.Empty;
            public string Section = "UNSORTED";
            public string Body = string.Empty;       // raw file content (incl. frontmatter)
            public bool IsHub;
            public readonly List<string> OutgoingLinks = new();  // keys
            public readonly List<string> Backlinks = new();      // keys
            public bool IsOrphan => !IsHub && OutgoingLinks.Count == 0 && Backlinks.Count == 0;
        }

        private static readonly Regex LinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);

        public readonly List<MemoryNode> Nodes = new();

        public int LinkCount { get; private set; }
        public int OrphanCount { get; private set; }

        public event Action? Changed;

        public MemoryNode? Find(string key)
            => Nodes.FirstOrDefault(n => n.Key == key);

        public void Scan()
        {
            MatrixSpacePaths.EnsureCreated();
            MigrateLegacyMemoryFile();

            Nodes.Clear();
            LinkCount = 0;
            OrphanCount = 0;

            foreach (var path in Directory.GetFiles(MatrixSpacePaths.MemoryDir, "*.md"))
            {
                var node = ParseFile(path);
                Nodes.Add(node);
            }

            // Resolve outgoing links to known keys and compute backlinks.
            var byKey = Nodes.ToDictionary(n => n.Key, n => n);
            foreach (var node in Nodes)
            {
                foreach (var link in node.OutgoingLinks)
                {
                    LinkCount++;
                    if (byKey.TryGetValue(link, out var target) && !target.Backlinks.Contains(node.Key))
                        target.Backlinks.Add(node.Key);
                }
            }

            OrphanCount = Nodes.Count(n => n.IsOrphan);
            Changed?.Invoke();
        }

        private static MemoryNode ParseFile(string path)
        {
            var node = new MemoryNode
            {
                FileName = Path.GetFileName(path),
                Key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant(),
                Body = File.ReadAllText(path),
            };
            node.Title = node.Key;

            var lines = node.Body.Split('\n');
            if (lines.Length > 0 && lines[0].TrimEnd() == "---")
            {
                for (var i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd();
                    if (line == "---")
                        break;

                    var colon = line.IndexOf(':');
                    if (colon <= 0)
                        continue;

                    var field = line[..colon].Trim().ToLowerInvariant();
                    var value = line[(colon + 1)..].Trim();
                    switch (field)
                    {
                        case "title" when value.Length > 0:
                            node.Title = value;
                            break;
                        case "section" when value.Length > 0:
                            node.Section = value.ToUpperInvariant();
                            break;
                        case "hub":
                            node.IsHub = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }

            foreach (Match match in LinkRegex.Matches(node.Body))
            {
                var key = match.Groups[1].Value.Trim().ToLowerInvariant();
                if (key.Length > 0 && !node.OutgoingLinks.Contains(key))
                    node.OutgoingLinks.Add(key);
            }

            return node;
        }

        /// <summary>First run: seed memory/hub.md from the legacy shared MEMORY.md.</summary>
        private static void MigrateLegacyMemoryFile()
        {
            if (Directory.GetFiles(MatrixSpacePaths.MemoryDir, "*.md").Length > 0)
                return;
            if (!File.Exists(MatrixSpacePaths.MemoryFile))
                return;

            var legacy = File.ReadAllText(MatrixSpacePaths.MemoryFile);
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("title: HUB");
            sb.AppendLine("section: HUB");
            sb.AppendLine("hub: true");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.Append(legacy);
            File.WriteAllText(Path.Combine(MatrixSpacePaths.MemoryDir, "hub.md"), sb.ToString());
        }

        public void Save(MemoryNode node, string newBody)
        {
            File.WriteAllText(Path.Combine(MatrixSpacePaths.MemoryDir, node.FileName), newBody);
            Scan();
        }

        public MemoryNode? Create(string title, string section)
        {
            var key = Slugify(title);
            if (key.Length == 0)
                return null;

            var path = Path.Combine(MatrixSpacePaths.MemoryDir, key + ".md");
            if (File.Exists(path))
                return Find(key);

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"title: {title}");
            sb.AppendLine($"section: {section.ToUpperInvariant()}");
            sb.AppendLine("---");
            sb.AppendLine();
            File.WriteAllText(path, sb.ToString());
            Scan();
            return Find(key);
        }

        public void Delete(MemoryNode node)
        {
            var path = Path.Combine(MatrixSpacePaths.MemoryDir, node.FileName);
            if (File.Exists(path))
                File.Delete(path);
            Scan();
        }

        private static string Slugify(string title)
        {
            var sb = new StringBuilder();
            foreach (var c in title.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '-')
                    sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }
    }
}
