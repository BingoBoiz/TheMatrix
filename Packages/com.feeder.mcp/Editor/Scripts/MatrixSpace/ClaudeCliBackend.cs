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
            StartTurnProcess(
                launchInfo.FileName,
                launchInfo.BuildArguments(BuildToolArguments()),
                prompt,
                AgentBackendCatalog.GetEnvironment(preset));
        }

        protected override IEnumerable<AgentEvent> ParseStdoutLine(string line)
            => ClaudeStreamJsonParser.ParseLine(line);

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
            var args = new StringBuilder("-p --output-format stream-json --verbose");

            var model = ModelOverride;
            if (string.IsNullOrWhiteSpace(model) || model == "default")
                model = MatrixSpaceSettings.Model.Value;
            if (!string.IsNullOrWhiteSpace(model) && model != "default")
                args.Append(" --model ").Append(model.Trim());

            if (MatrixSpaceSettings.SkipAllPermissions.Value)
            {
                args.Append(" --dangerously-skip-permissions");
            }
            else
            {
                var permissionMode = MatrixSpaceSettings.PermissionMode.Value;
                if (!string.IsNullOrWhiteSpace(permissionMode) && permissionMode != "default")
                    args.Append(" --permission-mode ").Append(permissionMode.Trim());

                if (MatrixSpaceSettings.AllowFeederMcpTools.Value)
                    args.Append(" --allowedTools \"").Append(MatrixSpaceSettings.FeederMcpAllowedTools).Append('"');
            }

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
