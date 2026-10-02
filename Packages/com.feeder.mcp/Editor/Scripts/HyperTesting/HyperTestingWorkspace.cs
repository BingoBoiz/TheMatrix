#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Feeder.MCP.Editor.HyperTesting
{
    // copies the journal templates, the orchestrator skill and one subagent file per clone into the project
    public static class HyperTestingWorkspace
    {
        const string IgnoreHeader = "# Matrix Hyper Testing (experimental): machine-specific files";

        static readonly string[] IgnoredPaths =
        {
            "/HyperTesting/Runs/",
            "/HyperTesting/Journal/ram-samples.csv",
            "/.claude/agents/hyper-playtester-*.md",
            "/.claude/skills/matrix-hyper-testing/",
        };

        static string TemplatesRoot
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HyperTestingWorkspace).Assembly);
                var root = info?.resolvedPath ?? Path.GetFullPath("Packages/com.feeder.mcp");
                return Path.Combine(root, "HyperTesting~");
            }
        }

        public static void Scaffold()
        {
            var source = Path.Combine(TemplatesRoot, "workspace");
            if (!Directory.Exists(source))
            {
                Debug.LogError($"{HyperTestingActivation.LogPrefix} template folder missing: {source}");
                return;
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(source.Length).TrimStart('\\', '/');
                var destination = Path.Combine(HyperTestingPaths.Workspace, relative);
                if (File.Exists(destination))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            Directory.CreateDirectory(HyperTestingPaths.Runs);
            new HyperTestingConfig().SaveIfMissing();
            Directory.CreateDirectory(HyperTestingPaths.AgentsFolder);
            WriteSkill();
            AppendGitIgnore();
        }

        public static void WriteSkill()
        {
            var template = Path.Combine(TemplatesRoot, "skill", "SKILL.md");
            if (!File.Exists(template))
            {
                Debug.LogError($"{HyperTestingActivation.LogPrefix} skill template missing: {template}");
                return;
            }
            Directory.CreateDirectory(HyperTestingPaths.SkillFolder);
            File.WriteAllText(Path.Combine(HyperTestingPaths.SkillFolder, "SKILL.md"), File.ReadAllText(template));
        }

        public static void WriteAgentFiles(HyperPoolState state)
        {
            if (!HyperTestingActivation.IsEnabled)
            {
                RemoveAgentFiles();
                return;
            }
            WriteSkill();
            var template = Path.Combine(TemplatesRoot, "agent", "hyper-playtester.md");
            if (!File.Exists(template))
            {
                Debug.LogError($"{HyperTestingActivation.LogPrefix} agent template missing: {template}");
                return;
            }

            Directory.CreateDirectory(HyperTestingPaths.AgentsFolder);
            DeleteAgentFiles();
            var text = File.ReadAllText(template);
            foreach (var entry in state.clones)
            {
                var content = text
                    .Replace("{{INDEX}}", entry.index.ToString())
                    .Replace("{{PORT}}", entry.port.ToString())
                    .Replace("{{CLONE_ROOT}}", entry.root.Replace('\\', '/'))
                    .Replace("{{ORIGIN_ROOT}}", HyperTestingPaths.ProjectRoot.Replace('\\', '/'));
                File.WriteAllText(Path.Combine(HyperTestingPaths.AgentsFolder, $"{HyperTestingPaths.AgentPrefix}{entry.index}.md"), content);
            }
        }

        public static void RemoveAgentFiles()
        {
            DeleteAgentFiles();
            if (Directory.Exists(HyperTestingPaths.SkillFolder))
                Directory.Delete(HyperTestingPaths.SkillFolder, true);
        }

        static void DeleteAgentFiles()
        {
            if (!Directory.Exists(HyperTestingPaths.AgentsFolder))
                return;
            foreach (var file in Directory.GetFiles(HyperTestingPaths.AgentsFolder, HyperTestingPaths.AgentPrefix + "*.md"))
                File.Delete(file);
        }

        static void AppendGitIgnore()
        {
            try
            {
                var path = Path.Combine(HyperTestingPaths.ProjectRoot, ".gitignore");
                var existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                var present = existing.Split('\n').Select(line => line.Trim()).ToHashSet();
                var missing = IgnoredPaths.Where(p => !present.Contains(p)).ToList();
                if (missing.Count == 0)
                    return;
                var eol = existing.Contains("\r\n") ? "\r\n" : "\n";
                var text = new StringBuilder();
                if (existing.Length > 0 && !existing.EndsWith("\n"))
                    text.Append(eol);
                text.Append(eol).Append(IgnoreHeader).Append(eol);
                foreach (var line in missing)
                    text.Append(line).Append(eol);
                File.AppendAllText(path, text.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{HyperTestingActivation.LogPrefix} could not update .gitignore: {ex.Message}");
            }
        }
    }
}
