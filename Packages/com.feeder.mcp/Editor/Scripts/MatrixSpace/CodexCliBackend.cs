#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Native Codex CLI integration. Prompts always travel through stdin (preserving multiline
    /// text verbatim), and `--json` exposes session, tool, usage, and result events.
    /// </summary>
    public sealed class CodexCliBackend : CliBackendBase
    {
        private readonly AgentBackendPreset _preset;
        private bool _sawResultThisTurn;
        private bool _sentFirstPrompt;

        public CodexCliBackend(AgentBackendPreset preset)
        {
            _preset = preset;
        }

        public override string Name => _preset.Label;

        public override void SendPrompt(string prompt)
        {
            ThrowIfBusy();

            var configuredExe = AgentBackendCatalog.ExecutableSetting(_preset).Value.Trim();
            if (configuredExe.Length == 0)
            {
                EnqueueProcessError("Codex: no executable configured. Open CONFIG > AGENTS and set it.");
                return;
            }

            var resolved = CliPathResolver.ResolveExecutable(configuredExe);
            if (resolved == null)
            {
                EnqueueProcessError(
                    $"Codex: executable '{configuredExe}' not found (checked path and PATH). " +
                    "Open CONFIG > AGENTS for the install guide.");
                return;
            }

            var mode = string.IsNullOrWhiteSpace(ModeOverride)
                ? MatrixSpaceSettings.DefaultAgentMode
                : ModeOverride!;
            var firstTurn = !_sentFirstPrompt && string.IsNullOrEmpty(SessionId);
            prompt = BuildPrompt(prompt, mode, firstTurn);

            var argsTemplate = AgentBackendCatalog.ArgumentsSetting(_preset).Value;
            argsTemplate = MigrateArguments(argsTemplate);
            argsTemplate = RenderArguments(argsTemplate, mode);
            argsTemplate = RemovePromptPlaceholder(argsTemplate);
            argsTemplate = EnsureJsonFlag(argsTemplate);
            argsTemplate = AddResume(argsTemplate);

            // A lone '-' tells both `codex exec` and `codex exec resume` to consume the
            // prompt from stdin. This avoids Windows command-line quoting and newline loss.
            var arguments = Regex.Replace(argsTemplate + " -", "  +", " ").Trim();
            var launchInfo = new CliLaunchInfo(resolved);

            _sawResultThisTurn = false;
            _sentFirstPrompt = true;
            StartTurnProcess(
                launchInfo.FileName,
                launchInfo.BuildArguments(arguments),
                prompt,
                AgentBackendCatalog.GetEnvironment(_preset));
        }

        protected override IEnumerable<AgentEvent> ParseStdoutLine(string line)
            => CodexStreamJsonParser.ParseLine(line);

        protected override void OnMainThreadEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
                case AgentEventKind.SystemInit:
                    if (!string.IsNullOrEmpty(evt.SessionId))
                        SessionId = evt.SessionId;
                    break;

                case AgentEventKind.Result:
                    _sawResultThisTurn = true;
                    break;

                case AgentEventKind.ProcessExited:
                    if (evt.ExitCode != 0)
                    {
                        evt.IsError = true;
                        var stderr = StderrTailText;
                        evt.Text = string.IsNullOrEmpty(stderr)
                            ? $"Codex exited with code {evt.ExitCode}."
                            : $"Codex exited with code {evt.ExitCode}:\n{stderr}";
                    }
                    else if (!_sawResultThisTurn)
                    {
                        evt.IsError = true;
                        evt.Text = "Codex exited without a turn.completed event. " +
                                   "Check the argument template in CONFIG > AGENTS.";
                    }
                    break;
            }
        }

        private string BuildPrompt(string prompt, string mode, bool firstTurn)
        {
            if (mode == "plan")
                return MatrixSpaceSettings.PlanOnlyPromptPrefix + "\n\n" + prompt;

            if (!firstTurn)
                return prompt;

            var parts = new List<string>();
            if (MatrixSpaceSettings.MatrixPersona.Value)
                parts.Add(MatrixSpaceSettings.MatrixPersonaSystemPrompt);
            parts.Add(MatrixSpaceSettings.CodingAgentPrompt);

            if (MatrixSpaceSettings.SharedMemory.Value)
            {
                MatrixSpacePaths.EnsureCreated();
                parts.Add(MatrixSpaceSettings.BuildSharedMemoryPrompt(AgentLabel ?? "AGENT"));
            }

            parts.Add(prompt);
            return string.Join("\n\n", parts);
        }

        private string RenderArguments(string argsTemplate, string mode)
        {
            var modelArg = AgentBackendCatalog.RenderModelArg(_preset, ModelOverride);
            argsTemplate = argsTemplate.Contains("{model}")
                ? argsTemplate.Replace("{model}", modelArg)
                : (modelArg.Length > 0 ? modelArg + " " + argsTemplate : argsTemplate);
            argsTemplate = argsTemplate.Replace("{mode}", AgentBackendCatalog.RenderModeArg(_preset, mode));
            return argsTemplate.Replace("{effort}",
                AgentBackendCatalog.RenderEffortArg(_preset, EffortOverride, ModelOverride));
        }

        private string AddResume(string argsTemplate)
        {
            if (string.IsNullOrEmpty(SessionId))
                return argsTemplate;

            var execIndex = argsTemplate.IndexOf("exec", StringComparison.Ordinal);
            if (execIndex < 0)
                return argsTemplate;

            return argsTemplate.Insert(execIndex + "exec".Length, " resume " + SessionId);
        }

        private static string MigrateArguments(string argsTemplate)
        {
            var trimmed = argsTemplate.Trim();
            if (trimmed == "exec --skip-git-repo-check {model} {prompt}" ||
                trimmed == "exec --skip-git-repo-check {mode} {effort} {model} {prompt}")
            {
                return AgentBackendCatalog.Get("codex").DefaultArguments;
            }

            return argsTemplate;
        }

        private static string RemovePromptPlaceholder(string argsTemplate)
            => argsTemplate.Replace("{prompt}", string.Empty);

        private static string EnsureJsonFlag(string argsTemplate)
            => Regex.IsMatch(argsTemplate, @"(^|\s)--json(?=\s|$)")
                ? argsTemplate
                : argsTemplate + " --json";
    }
}
