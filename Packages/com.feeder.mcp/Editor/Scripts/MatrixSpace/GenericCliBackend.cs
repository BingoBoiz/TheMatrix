#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Runs any terminal AI agent (Codex/ChatGPT, Gemini, Kimi, custom) one process per turn.
    /// stdout is streamed into the transcript as plain text. Presets with
    /// <see cref="AgentBackendPreset.SupportsExecResume"/> (Codex) keep conversation context
    /// via "exec resume &lt;session-id&gt;"; the rest are stateless.
    /// </summary>
    public sealed class GenericCliBackend : CliBackendBase
    {
        /// <summary>Matches the "session id: &lt;uuid&gt;" header codex exec prints.</summary>
        private static readonly System.Text.RegularExpressions.Regex SessionIdPattern =
            new(@"session[ _]?id:?\s*([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private readonly AgentBackendPreset _preset;
        private bool _receivedOutputThisTurn;
        private bool _sentFirstPrompt;

        public GenericCliBackend(AgentBackendPreset preset)
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
                EnqueueProcessError($"{_preset.Label}: no executable configured. Open CONFIG > AGENTS and set it.");
                return;
            }

            var resolved = CliPathResolver.ResolveExecutable(configuredExe);
            if (resolved == null)
            {
                EnqueueProcessError(
                    $"{_preset.Label}: executable '{configuredExe}' not found (checked path and PATH). " +
                    "Open CONFIG > AGENTS for the install guide.");
                return;
            }

            var mode = string.IsNullOrWhiteSpace(ModeOverride) ? MatrixSpaceSettings.DefaultAgentMode : ModeOverride!;
            var firstTurn = !_sentFirstPrompt && string.IsNullOrEmpty(SessionId);

            // Generic CLIs have no system-prompt flag; instructions ride on the prompt text.
            if (mode == "plan")
                prompt = MatrixSpaceSettings.PlanOnlyPromptPrefix + "\n\n" + prompt;
            else if (firstTurn && AgentBackendCatalog.SupportsModes(_preset))
                prompt = MatrixSpaceSettings.CodingAgentPrompt + "\n\n" + prompt;

            if (MatrixSpaceSettings.SharedMemory.Value)
            {
                MatrixSpacePaths.EnsureCreated();
                if (firstTurn)
                    prompt = MatrixSpaceSettings.BuildSharedMemoryPrompt(AgentLabel ?? "AGENT") + "\n\n" + prompt;
            }

            var argsTemplate = AgentBackendCatalog.ArgumentsSetting(_preset).Value;

            // Migrate a persisted pre-mode/effort codex template to the current default.
            if (_preset.Id == "codex" && argsTemplate.Trim() == "exec --skip-git-repo-check {model} {prompt}")
                argsTemplate = _preset.DefaultArguments;

            // Codex-style conversation resume: "exec" becomes "exec resume <id>" after turn 1.
            if (_preset.SupportsExecResume && !string.IsNullOrEmpty(SessionId))
            {
                var execIndex = argsTemplate.IndexOf("exec", System.StringComparison.Ordinal);
                if (execIndex >= 0)
                    argsTemplate = argsTemplate.Insert(execIndex + "exec".Length, " resume " + SessionId);
            }

            var modelArg = AgentBackendCatalog.RenderModelArg(_preset, ModelOverride);
            argsTemplate = argsTemplate.Contains("{model}")
                ? argsTemplate.Replace("{model}", modelArg)
                : (modelArg.Length > 0 ? modelArg + " " + argsTemplate : argsTemplate);
            argsTemplate = argsTemplate.Replace("{mode}", AgentBackendCatalog.RenderModeArg(_preset, mode));
            argsTemplate = argsTemplate.Replace("{effort}",
                AgentBackendCatalog.RenderEffortArg(_preset, EffortOverride, ModelOverride));

            string arguments;
            string? stdinText;
            if (argsTemplate.Contains("{prompt}"))
            {
                arguments = argsTemplate.Replace("{prompt}", QuoteArgument(prompt));
                stdinText = null;
            }
            else
            {
                arguments = argsTemplate;
                stdinText = prompt;
            }

            arguments = System.Text.RegularExpressions.Regex.Replace(arguments, "  +", " ").Trim();

            var launchInfo = new CliLaunchInfo(resolved);
            _receivedOutputThisTurn = false;
            _sentFirstPrompt = true;
            StartTurnProcess(
                launchInfo.FileName,
                launchInfo.BuildArguments(arguments),
                stdinText,
                AgentBackendCatalog.GetEnvironment(_preset));
        }

        protected override IEnumerable<AgentEvent> ParseStdoutLine(string line)
        {
            if (TryParseSessionId(line, out var sessionEvent))
                yield return sessionEvent;

            yield return new AgentEvent(AgentEventKind.AssistantText)
            {
                Text = line + "\n",
                IsDelta = true,
            };
        }

        protected override void OnStderrLineReceived(string line)
        {
            // codex exec streams its header (including the session id) to stderr.
            if (TryParseSessionId(line, out var sessionEvent))
                EnqueueEvent(sessionEvent);
        }

        private bool TryParseSessionId(string line, out AgentEvent sessionEvent)
        {
            sessionEvent = null!;
            if (!_preset.SupportsExecResume || !string.IsNullOrEmpty(SessionId))
                return false;

            var match = SessionIdPattern.Match(line);
            if (!match.Success)
                return false;

            sessionEvent = new AgentEvent(AgentEventKind.SystemInit) { SessionId = match.Groups[1].Value };
            return true;
        }

        protected override void OnMainThreadEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
                case AgentEventKind.SystemInit:
                    if (evt.SessionId != null)
                        SessionId = evt.SessionId;
                    break;

                case AgentEventKind.AssistantText:
                    _receivedOutputThisTurn = true;
                    break;

                case AgentEventKind.ProcessExited:
                    if (evt.ExitCode != 0)
                    {
                        evt.IsError = true;
                        var stderr = StderrTailText;
                        evt.Text = string.IsNullOrEmpty(stderr)
                            ? $"{_preset.Label} exited with code {evt.ExitCode}."
                            : $"{_preset.Label} exited with code {evt.ExitCode}:\n{stderr}";
                    }
                    else if (!_receivedOutputThisTurn)
                    {
                        evt.IsError = true;
                        evt.Text = $"{_preset.Label} produced no output. Check the argument template in CONFIG > AGENTS.";
                    }
                    break;
            }
        }

        private static string QuoteArgument(string value)
            => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
