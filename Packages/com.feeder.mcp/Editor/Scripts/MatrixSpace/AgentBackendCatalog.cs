#nullable enable

using System;
using System.Collections.Generic;
using Feeder.MCP.Editor.Persistence;

namespace Feeder.MCP.Editor.MatrixSpace
{
    public sealed class AgentBackendPreset
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        /// <summary>Executable name (resolved via PATH) or full path. Editable in settings.</summary>
        public string DefaultExecutable = string.Empty;
        /// <summary>
        /// Argument template. "{prompt}" is replaced with the quoted prompt; when absent,
        /// the prompt is piped to stdin instead. "{model}" (optional) is replaced with the
        /// rendered model argument. Editable in settings.
        /// </summary>
        public string DefaultArguments = string.Empty;
        /// <summary>How to pass a model id, e.g. "-m {model}". Empty = unsupported.</summary>
        public string ModelArgTemplate = string.Empty;
        /// <summary>Comma-separated model suggestions; "default" means no --model flag.</summary>
        public string DefaultModels = "default";
        /// <summary>Claude gets the full stream-json backend; everything else runs generic.</summary>
        public bool IsClaude;
        /// <summary>Install page opened by the INSTALL button.</summary>
        public string InstallUrl = string.Empty;
        /// <summary>Arguments passed when opening a login terminal (empty = just run the CLI).</summary>
        public string LoginArgs = string.Empty;
        /// <summary>Instruction shown next to the LOGIN button.</summary>
        public string LoginHint = string.Empty;
        public string? Note;
    }

    /// <summary>
    /// Registry of per-pane agent backends. Claude Code is fully integrated (streaming,
    /// resume, MCP permissions); the others run through <see cref="GenericCliBackend"/>.
    /// </summary>
    public static class AgentBackendCatalog
    {
        public const string DefaultId = "claude";

        public static readonly AgentBackendPreset[] Presets =
        {
            new()
            {
                Id = "claude",
                Label = "Claude Code",
                IsClaude = true,
                ModelArgTemplate = "--model {model}",
                DefaultModels = "default,sonnet,opus,haiku",
                InstallUrl = "https://claude.com/claude-code",
                LoginArgs = "",
                LoginHint = "In the terminal that opens, type /login and follow the browser flow.",
            },
            new()
            {
                Id = "codex",
                Label = "Codex (ChatGPT)",
                DefaultExecutable = "codex",
                DefaultArguments = "exec --skip-git-repo-check {model} {prompt}",
                ModelArgTemplate = "-m {model}",
                DefaultModels = "default,gpt-5.2-codex,gpt-5.1-codex-max",
                InstallUrl = "https://developers.openai.com/codex/cli",
                LoginArgs = "login",
                LoginHint = "Completes ChatGPT sign-in in your browser.",
                Note = "OpenAI Codex CLI. Each turn is stateless.",
            },
            new()
            {
                Id = "gemini",
                Label = "Gemini CLI",
                DefaultExecutable = "gemini",
                DefaultArguments = "{model} -p {prompt}",
                ModelArgTemplate = "-m {model}",
                DefaultModels = "default,gemini-2.5-pro,gemini-2.5-flash",
                InstallUrl = "https://github.com/google-gemini/gemini-cli",
                LoginArgs = "",
                LoginHint = "Sign-in starts automatically on first run.",
                Note = "Google Gemini CLI. Each turn is stateless.",
            },
            new()
            {
                Id = "kimi",
                Label = "Kimi CLI",
                DefaultExecutable = "kimi",
                DefaultArguments = "{model} --print {prompt}",
                ModelArgTemplate = "--model {model}",
                DefaultModels = "default,k3",
                InstallUrl = "https://github.com/MoonshotAI/kimi-cli",
                LoginHint = "Run the CLI once to configure your Moonshot account.",
                Note = "Moonshot Kimi CLI. Adjust the arguments to your installed version.",
            },
            new()
            {
                Id = "custom",
                Label = "Custom CLI",
                DefaultExecutable = "",
                DefaultArguments = "{prompt}",
                ModelArgTemplate = "{model}",
                DefaultModels = "default",
                Note = "Any terminal AI agent: set the executable and argument template.",
            },
        };

        public static AgentBackendPreset Get(string? id)
        {
            foreach (var preset in Presets)
            {
                if (preset.Id == id)
                    return preset;
            }

            return Presets[0];
        }

        public static int IndexOf(string? id)
        {
            for (var i = 0; i < Presets.Length; i++)
            {
                if (Presets[i].Id == id)
                    return i;
            }

            return 0;
        }

        public static PlayerPrefsString ExecutableSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Exe", preset.DefaultExecutable);

        public static PlayerPrefsString ArgumentsSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Args", preset.DefaultArguments);

        public static PlayerPrefsString ModelsSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Models", preset.DefaultModels);

        /// <summary>Multiline "KEY=VALUE" pairs injected into the agent process environment.</summary>
        public static PlayerPrefsString EnvironmentSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Env", string.Empty);

        public static List<string> GetModels(AgentBackendPreset preset)
        {
            var models = new List<string>();
            foreach (var raw in ModelsSetting(preset).Value.Split(','))
            {
                var model = raw.Trim();
                if (model.Length > 0)
                    models.Add(model);
            }

            if (models.Count == 0)
                models.Add("default");
            return models;
        }

        public static List<KeyValuePair<string, string>> GetEnvironment(AgentBackendPreset preset)
        {
            var env = new List<KeyValuePair<string, string>>();
            var raw = EnvironmentSetting(preset).Value;
            if (string.IsNullOrWhiteSpace(raw))
                return env;

            foreach (var rawLine in raw.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                env.Add(new KeyValuePair<string, string>(
                    line.Substring(0, eq).Trim(),
                    line.Substring(eq + 1).Trim()));
            }

            return env;
        }

        /// <summary>Renders the model argument (e.g. "-m gpt-5.2"), or "" for default/none.</summary>
        public static string RenderModelArg(AgentBackendPreset preset, string? model)
        {
            if (string.IsNullOrWhiteSpace(model) || model == "default" || preset.ModelArgTemplate.Length == 0)
                return string.Empty;

            return preset.ModelArgTemplate.Replace("{model}", model!.Trim());
        }

        /// <summary>Resolved executable path for this preset (user setting or default), or null.</summary>
        public static string? ResolveExecutablePath(AgentBackendPreset preset)
        {
            if (preset.IsClaude)
                return ClaudeCliLocator.Locate();

            return CliPathResolver.ResolveExecutable(ExecutableSetting(preset).Value);
        }

        public static IAgentBackend Create(string? backendId)
        {
            var preset = Get(backendId);
            return preset.IsClaude
                ? new ClaudeCliBackend()
                : new GenericCliBackend(preset);
        }
    }
}
