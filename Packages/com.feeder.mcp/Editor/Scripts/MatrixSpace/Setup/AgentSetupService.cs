#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// Runs agent setup pipelines off-thread and streams progress to the UI, mirroring
    /// <see cref="AgentStatusService"/>'s context-capture pattern. Runs are serialized —
    /// concurrent npm/winget invocations step on each other.
    /// </summary>
    public static class AgentSetupService
    {
        private static readonly Dictionary<string, AgentSetupState> _states = new();
        private static bool _running;

        /// <summary>Raised on the main thread whenever any setup state changes.</summary>
        public static event Action? Changed;

        public static bool IsAnyRunning => _running;

        public static AgentSetupState GetState(AgentBackendPreset preset)
        {
            if (!_states.TryGetValue(preset.Id, out var state))
            {
                state = new AgentSetupState();
                _states[preset.Id] = state;
            }

            return state;
        }

        public static void RunSetup(AgentBackendPreset preset) => RunMany(new[] { preset });

        public static void RunSetupAll()
        {
            var presets = new List<AgentBackendPreset>();
            foreach (var preset in AgentBackendCatalog.Presets)
            {
                if (AgentSetupCatalog.Get(preset.Id) != null)
                    presets.Add(preset);
            }

            RunMany(presets);
        }

        private static void RunMany(IReadOnlyList<AgentBackendPreset> presets)
        {
            if (_running || presets.Count == 0)
                return;

            MatrixActivation.Enable();

            var ui = SynchronizationContext.Current;
            _running = true;

            foreach (var preset in presets)
            {
                var state = GetState(preset);
                state.Phase = AgentSetupPhase.Queued;
                state.CurrentStep = "Queued…";
                state.Log.Clear();
            }

            Changed?.Invoke();

            Task.Run(() =>
            {
                foreach (var preset in presets)
                {
                    try
                    {
                        RunPipeline(preset, ui);
                    }
                    catch (Exception ex)
                    {
                        Post(ui, () =>
                        {
                            var state = GetState(preset);
                            state.Phase = AgentSetupPhase.Failed;
                            state.CurrentStep = string.Empty;
                            state.Log.Add($"Unexpected error: {ex.Message}");
                            Debug.LogError($"[MatrixSpace:Setup:{preset.Id}] Unexpected error — full stack below.");
                            Debug.LogException(ex);
                        });
                    }
                }

                Post(ui, () => { _running = false; });
            });
        }

        /// <summary>Worker-thread pipeline; all state writes are marshalled to the main thread.</summary>
        private static void RunPipeline(AgentBackendPreset preset, SynchronizationContext? ui)
        {
            var setup = AgentSetupCatalog.Get(preset.Id);
            if (setup == null)
                return;

            var ctx = new SetupContext(preset,
                line => Post(ui, () => AppendLog(preset, line)),
                ui);

            var steps = setup.BuildSteps();
            Post(ui, () =>
            {
                GetState(preset).Phase = AgentSetupPhase.Running;
                AppendLog(preset, $"── Setup {preset.Label} (id: {preset.Id}, {steps.Count} steps) ──");
            });

            var succeeded = true;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var header = $"▶ Step {i + 1}/{steps.Count}: {step.Name}";
                Post(ui, () =>
                {
                    GetState(preset).CurrentStep = step.Name;
                    AppendLog(preset, header);
                });

                bool ok;
                if (step.MainThread && ui != null)
                {
                    var result = false;
                    ui.Send(_ => result = step.Run(ctx), null);
                    ok = result;
                }
                else
                {
                    ok = step.Run(ctx);
                }

                if (!ok)
                {
                    succeeded = false;
                    break;
                }
            }

            Post(ui, () =>
            {
                var state = GetState(preset);
                state.Phase = succeeded ? AgentSetupPhase.Succeeded : AgentSetupPhase.Failed;
                state.CurrentStep = string.Empty;
                if (succeeded && !ctx.SkipInstall)
                {
                    // Freshly installed ⇒ certainly not signed in yet: hand the user straight
                    // to the vendor's own login flow (terminal → browser). Already-installed
                    // CLIs are likely signed in, so no window is popped for them.
                    AppendLog(preset, AgentLoginLauncher.Open(preset)
                        ? "Login terminal opened — complete sign-in in your browser. That's the only manual step."
                        : "Install done, but the login terminal could not be opened — press LOGIN to sign in.");
                }
                else
                {
                    AppendLog(preset, succeeded
                        ? "Setup complete. If the status shows AUTH REQUIRED, press LOGIN to sign in — that's the only manual step."
                        : "Setup incomplete — see the log above, then press SETUP to retry.");
                }

                // On failure, one copyable console entry holds the whole run.
                if (!succeeded)
                    Debug.LogError($"[MatrixSpace:Setup:{preset.Id}] Setup FAILED — full log:\n" +
                                   string.Join("\n", state.Log));

                // Re-probe so the READY / AUTH REQUIRED status line reflects the fresh install.
                AgentStatusService.Probe(preset);
            });
        }

        /// <summary>
        /// Main-thread only: timestamps the line, stores it for the SETUP LOG foldout, and
        /// mirrors it to the Unity Console so progress/stuck points are visible and copyable.
        /// </summary>
        private static void AppendLog(AgentBackendPreset preset, string line)
        {
            var stamped = $"{DateTime.Now:HH:mm:ss} {line}";
            GetState(preset).Log.Add(stamped);
            Debug.Log($"[MatrixSpace:Setup:{preset.Id}] {stamped}");
        }

        private static void Post(SynchronizationContext? ui, Action action)
        {
            if (ui != null)
            {
                ui.Post(_ =>
                {
                    action();
                    Changed?.Invoke();
                }, null);
            }
            else
            {
                action();
            }
        }
    }
}
