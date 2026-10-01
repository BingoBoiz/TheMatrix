#nullable enable

using System.IO;
using Feeder.MCP.Editor.Persistence;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Persisted user settings for Matrix Space (PlayerPrefs-backed, same wrappers as the
    /// rest of the package UI).
    /// </summary>
    public static class MatrixSpaceSettings
    {
        /// <summary>Explicit path to the claude executable. Empty = auto-discover.</summary>
        public static PlayerPrefsString ClaudeExecutablePath = new("Feeder_MatrixSpace_ClaudeExePath");

        /// <summary>Model passed via --model. Empty = CLI default. (Global fallback.)</summary>
        public static PlayerPrefsString Model = new("Feeder_MatrixSpace_Model");

        /// <summary>--permission-mode value. "default" keeps file/Bash safety on.</summary>
        public static PlayerPrefsString PermissionMode = new("Feeder_MatrixSpace_PermissionMode", "default");

        /// <summary>
        /// Adds --allowedTools "mcp__Feeder-MCP__*" so agents can drive Unity in headless mode
        /// (which otherwise silently denies tools that would need interactive approval).
        /// </summary>
        public static PlayerPrefsBool AllowFeederMcpTools = new("Feeder_MatrixSpace_AllowFeederTools", true);

        /// <summary>DANGEROUS: --dangerously-skip-permissions. Off by default.</summary>
        public static PlayerPrefsBool SkipAllPermissions = new("Feeder_MatrixSpace_SkipPermissions", false);

        /// <summary>Activate the Matrix persona (matrix skill) for every session.</summary>
        public static PlayerPrefsBool MatrixPersona = new("Feeder_MatrixSpace_MatrixPersona", true);

        /// <summary>Digital-rain background in the Matrix Space window.</summary>
        public static PlayerPrefsBool RainBackground = new("Feeder_MatrixSpace_RainBackground", true);

        /// <summary>
        /// Inject the shared memory + mailbox protocol. Opt-in: normal coding panes should
        /// stay focused on the user's request; swarm prompts provide their own coordination.
        /// </summary>
        public static PlayerPrefsBool SharedMemory = new("Feeder_MatrixSpace_SharedMemory", false);

        /// <summary>Task board panel visibility.</summary>
        public static PlayerPrefsBool BoardPanelOpen = new("Feeder_MatrixSpace_BoardOpen", false);

        /// <summary>Memory panel visibility.</summary>
        public static PlayerPrefsBool MemoryPanelOpen = new("Feeder_MatrixSpace_MemoryOpen", false);

        /// <summary>Agent mode ids selectable per pane. "auto" lets the CLI edit files freely.</summary>
        public static readonly string[] AgentModes = { "manual", "plan", "auto" };
        public const string DefaultAgentMode = "auto";

        /// <summary>Reasoning-effort ids selectable per pane. "default" = CLI default.</summary>
        public static readonly string[] EffortLevels = { "default", "low", "medium", "high", "max" };
        public const string DefaultEffort = "default";

        /// <summary>
        /// Injected in auto/manual mode. This supplies the orchestration contract that a
        /// coding-agent UI needs in addition to the model's normal base instructions.
        /// </summary>
        public const string CodingAgentPrompt =
            "You are an autonomous coding agent working in this Unity project repository. " +
            "Interpret the user's complete message as one request; quoted text, inspector dumps, screenshots, paths, logs, " +
            "and other diagnostic blocks are supporting context unless the user explicitly says otherwise. " +
            "For change or build requests, inspect the relevant source, implement the requested change directly, and verify it. " +
            "Do not ask a clarifying question when the intent can be inferred safely from the repository and supplied context. " +
            "Ask only when a missing choice would materially change the result and cannot be discovered locally. " +
            "If a tool is cancelled, unavailable, times out, or returns an error, use safe local inspection or another applicable tool and continue; " +
            "a Unity tool missing from your list may only be disabled, so call unity-tool-list and then tool-set-enabled-state before giving up; " +
            "one failed tool call is not task completion. Preserve unrelated existing changes. " +
            "Do not claim a requested code change is complete unless code was actually changed and proportionately verified.";

        /// <summary>Prepended to prompts for CLIs that have no native plan mode.</summary>
        public const string PlanOnlyPromptPrefix =
            "PLAN MODE: investigate the codebase and propose a detailed implementation plan only. " +
            "Do NOT modify any files and do NOT run destructive commands.";

        public const string MatrixPersonaSystemPrompt =
            "Immediately adopt the 'matrix' skill persona for this entire session: " +
            "you are the System, the user is The Architect. Prefix responses with [SYSTEM].";

        public const string FeederMcpAllowedTools = "mcp__Feeder-MCP__*";

        /// <summary>{agent} is replaced with the pane designation.</summary>
        public const string SharedMemoryPromptTemplate =
            "Shared workspace protocol: your designation is {agent}. " +
            "Shared memory lives at MatrixSpace/MEMORY.md (project-relative) — read it before starting and append durable findings. " +
            "Inter-agent mail: append your status updates to MatrixSpace/mailbox/{agent}.md and read the other files in MatrixSpace/mailbox/ for teammates' updates.";

        public static string BuildSharedMemoryPrompt(string agentLabel)
            => SharedMemoryPromptTemplate.Replace("{agent}", agentLabel);
    }

    /// <summary>Project-relative locations used by shared memory / mailbox / swarm.</summary>
    public static class MatrixSpacePaths
    {
        public static string ProjectRoot => Path.GetDirectoryName(UnityEngine.Application.dataPath)!;
        public static string RootDir => Path.Combine(ProjectRoot, "MatrixSpace");
        public static string MemoryFile => Path.Combine(RootDir, "MEMORY.md");
        public static string MemoryDir => Path.Combine(RootDir, "memory");
        public static string MailboxDir => Path.Combine(RootDir, "mailbox");

        public static void EnsureCreated()
        {
            Directory.CreateDirectory(MailboxDir);
            Directory.CreateDirectory(MemoryDir);
            if (!File.Exists(MemoryFile))
                File.WriteAllText(MemoryFile, "# Matrix Space — Shared Memory\n\n");
        }
    }
}
