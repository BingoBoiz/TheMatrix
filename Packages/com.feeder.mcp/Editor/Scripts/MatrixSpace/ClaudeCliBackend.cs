#nullable enable

using System.Collections.Generic;
using System.Text;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Runs Claude Code headless, one short-lived process per turn:
    /// `claude -p --output-format stream-json --verbose [--resume id]`, prompt via stdin.
    /// </summary>
    public sealed class ClaudeCliBackend : CliBackendBase
    {
        private bool _sawResultThisTurn;
        private ClaudeStreamJsonParser.State _parserState = new();

        public override string Name => "Claude Code";

        public override void SendPrompt(string prompt)
        {
            ThrowIfBusy();

            if (!ClaudeCliLocator.TryGetLaunchInfo(out var launchInfo, out var locateError))
            {
                EnqueueProcessError(locateError);
                return;
            }

            if (MatrixSpaceSettings.SharedMemory.Value)
                MatrixSpacePaths.EnsureCreated();

            var preset = AgentBackendCatalog.Get("claude");
            _sawResultThisTurn = false;
            _parserState = new ClaudeStreamJsonParser.State();
            StartTurnProcess(
                launchInfo.FileName,
                launchInfo.BuildArguments(BuildToolArguments()),
                prompt,
                AgentBackendCatalog.GetEnvironment(preset));
        }

        protected override IEnumerable<AgentEvent> ParseStdoutLine(string line)
            => ClaudeStreamJsonParser.ParseLine(line, _parserState);

        protected override void OnMainThreadEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
                case AgentEventKind.SystemInit:
                case AgentEventKind.Result:
                    if (evt.SessionId != null)
                        SessionId = evt.SessionId;
                    if (evt.Kind == AgentEventKind.Result)
                        _sawResultThisTurn = true;
                    break;

                case AgentEventKind.ProcessExited:
                    // A clean turn already reported via Result; only surface abnormal exits.
                    if (!_sawResultThisTurn && evt.ExitCode != 0)
                    {
                        evt.IsError = true;
                        var stderr = StderrTailText;
                        evt.Text = string.IsNullOrEmpty(stderr)
                            ? $"claude exited with code {evt.ExitCode}."
                            : $"claude exited with code {evt.ExitCode}:\n{stderr}";
                    }
                    break;
            }
        }

        private string BuildToolArguments()
        {
            var args = new StringBuilder("-p --output-format stream-json --include-partial-messages --verbose");

            var model = ModelOverride;
            if (string.IsNullOrWhiteSpace(model) || model == "default")
                model = MatrixSpaceSettings.Model.Value;
            if (!string.IsNullOrWhiteSpace(model) && model != "default")
                args.Append(" --model ").Append(model.Trim());

            var mode = string.IsNullOrWhiteSpace(ModeOverride) ? MatrixSpaceSettings.DefaultAgentMode : ModeOverride!;
            if (MatrixSpaceSettings.SkipAllPermissions.Value)
            {
                args.Append(" --dangerously-skip-permissions");
            }
            else if (mode == "auto")
            {
                // Headless -p auto-denies any tool needing approval, so "auto" must bypass
                // to let the agent actually edit files and run commands.
                args.Append(" --permission-mode bypassPermissions");
            }
            else
            {
                if (mode == "plan")
                {
                    args.Append(" --permission-mode plan");
                }
                else
                {
                    var permissionMode = MatrixSpaceSettings.PermissionMode.Value;
                    if (!string.IsNullOrWhiteSpace(permissionMode) && permissionMode != "default")
                        args.Append(" --permission-mode ").Append(permissionMode.Trim());
                }

                if (MatrixSpaceSettings.AllowFeederMcpTools.Value)
                    args.Append(" --allowedTools \"").Append(MatrixSpaceSettings.FeederMcpAllowedTools).Append('"');
            }

            var effort = EffortOverride;
            if (!string.IsNullOrWhiteSpace(effort) && effort != "default")
                args.Append(" --effort ").Append(effort!.Trim());

            if (SessionId != null)
            {
                args.Append(" --resume ").Append(SessionId);
            }
            else
            {
                // System prompt only needs to be injected on the first turn; --resume keeps it.
                var systemParts = new List<string>();
                if (MatrixSpaceSettings.MatrixPersona.Value)
                    systemParts.Add(MatrixSpaceSettings.MatrixPersonaSystemPrompt);
                if (mode != "plan")
                    systemParts.Add(MatrixSpaceSettings.CodingAgentPrompt);
                if (MatrixSpaceSettings.SharedMemory.Value)
                    systemParts.Add(MatrixSpaceSettings.BuildSharedMemoryPrompt(AgentLabel ?? "AGENT"));

                if (systemParts.Count > 0)
                {
                    args.Append(" --append-system-prompt \"")
                        .Append(string.Join(" ", systemParts).Replace("\"", "\\\""))
                        .Append('"');
                }
            }

            return args.ToString();
        }
    }
}
