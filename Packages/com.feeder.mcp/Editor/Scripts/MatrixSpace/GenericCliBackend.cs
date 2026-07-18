#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Runs any terminal AI agent (Codex/ChatGPT, Gemini, Kimi, custom) one process per turn.
    /// stdout is streamed into the transcript as plain text. Stateless: these CLIs get no
    /// conversation resume — each prompt is a fresh run.
    /// </summary>
    public sealed class GenericCliBackend : CliBackendBase
    {
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

            // Generic CLIs have no system-prompt flag; the shared protocol rides on turn 1.
            if (MatrixSpaceSettings.SharedMemory.Value)
            {
                MatrixSpacePaths.EnsureCreated();
                if (!_sentFirstPrompt)
                    prompt = MatrixSpaceSettings.BuildSharedMemoryPrompt(AgentLabel ?? "AGENT") + "\n\n" + prompt;
            }

            var argsTemplate = AgentBackendCatalog.ArgumentsSetting(_preset).Value;
            var modelArg = AgentBackendCatalog.RenderModelArg(_preset, ModelOverride);
            argsTemplate = argsTemplate.Contains("{model}")
                ? argsTemplate.Replace("{model}", modelArg)
                : (modelArg.Length > 0 ? modelArg + " " + argsTemplate : argsTemplate);

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
            yield return new AgentEvent(AgentEventKind.AssistantText)
            {
                Text = line + "\n",
                IsDelta = true,
            };
        }

        protected override void OnMainThreadEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
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
