#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    public enum AgentSetupPhase
    {
        Idle,
        Queued,
        Running,
        Succeeded,
        Failed,
    }

    /// <summary>Per-agent setup progress shown in CONFIG &gt; AGENTS. Mutated on the main thread only.</summary>
    public sealed class AgentSetupState
    {
        public AgentSetupPhase Phase = AgentSetupPhase.Idle;
        public string CurrentStep = string.Empty;
        public readonly List<string> Log = new();
    }

    /// <summary>
    /// Shared per-run context handed to every <see cref="SetupStep"/>. Steps run on a worker
    /// thread unless marked <see cref="SetupStep.MainThread"/>; anything Unity-API-bound must
    /// go through <see cref="Ui"/>.
    /// </summary>
    public sealed class SetupContext
    {
        public SetupContext(AgentBackendPreset preset, Action<string> log, SynchronizationContext? ui)
        {
            Preset = preset;
            Log = log;
            Ui = ui;
        }

        public AgentBackendPreset Preset { get; }
        /// <summary>Thread-safe progress logger (the service marshals to the UI).</summary>
        public Action<string> Log { get; }
        /// <summary>Main-thread context captured at run start; null in headless edge cases.</summary>
        public SynchronizationContext? Ui { get; }

        /// <summary>Set when the CLI is already present — install steps become no-ops.</summary>
        public bool SkipInstall;
        /// <summary>Absolute npm.cmd path resolved by the Node prerequisite step.</summary>
        public string? NpmPath;
        /// <summary>Absolute CLI path once resolved (pre-existing or freshly installed).</summary>
        public string? ResolvedExecutablePath;

        /// <summary>Runs an action on the main thread and blocks until it completes.</summary>
        public void RunOnUi(Action action)
        {
            if (Ui != null)
                Ui.Send(_ => action(), null);
            else
                action();
        }

        public void OpenUrl(string url) => RunOnUi(() => UnityEngine.Application.OpenURL(url));
    }

    /// <summary>One named unit of setup work. Return false to abort the remaining steps.</summary>
    public readonly struct SetupStep
    {
        public readonly string Name;
        public readonly Func<SetupContext, bool> Run;
        /// <summary>True when the step touches Unity APIs (PlayerPrefs, configurators, …).</summary>
        public readonly bool MainThread;

        public SetupStep(string name, Func<SetupContext, bool> run, bool mainThread = false)
        {
            Name = name;
            Run = run;
            MainThread = mainThread;
        }
    }

    /// <summary>Automated installer for one agent backend; one implementation per vendor.</summary>
    public interface IAgentSetup
    {
        /// <summary>Matches <see cref="AgentBackendPreset.Id"/>.</summary>
        string PresetId { get; }
        IReadOnlyList<SetupStep> BuildSteps();
    }
}
