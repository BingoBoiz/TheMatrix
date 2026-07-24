#nullable enable

using System.IO;
using Feeder.MCP.Editor.Persistence;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Persisted user settings for Matrix Space (PlayerPrefs-backed, same wrappers as the
    /// rest of the connector UI).
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

        /// <summary>Inject the shared memory + mailbox protocol into every session.</summary>
        public static PlayerPrefsBool SharedMemory = new("Feeder_MatrixSpace_SharedMemory", true);

        /// <summary>Task board panel visibility.</summary>
        public static PlayerPrefsBool BoardPanelOpen = new("Feeder_MatrixSpace_BoardOpen", false);

        /// <summary>Memory panel visibility.</summary>
        public static PlayerPrefsBool MemoryPanelOpen = new("Feeder_MatrixSpace_MemoryOpen", false);

        public const string MatrixPersonaSystemPrompt =
            "Immediately adopt the 'matrix' skill persona for this entire session: " +
            "you are the System, the user is The Architect. Prefix responses with [SYSTEM]. " +
            "To save tokens only a small core set of Unity tools is enabled up front. " +
            "When a task needs a Unity operation you don't currently have, call `unity-tool-list` " +
            "to find the right tool, then `tool-set-enabled-state` to enable it before using it.";

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
