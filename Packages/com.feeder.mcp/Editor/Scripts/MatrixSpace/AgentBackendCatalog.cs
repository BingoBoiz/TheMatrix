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
        /// <summary>Comma-separated offline fallback models; "default" means no --model flag.</summary>
        public string DefaultModels = "default";
        /// <summary>models.dev provider key ("anthropic", "openai", ...); null = no dynamic catalog.</summary>
        public string? ModelsDevProviderId;
        /// <summary>Args that make the CLI print its model ids (e.g. "--list-models"); empty = unsupported.</summary>
        public string ListModelsArgs = string.Empty;
        /// <summary>Case-insensitive regex removing catalog models this CLI cannot use; empty = keep all.</summary>
        public string ModelExcludePattern = string.Empty;
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
                ModelsDevProviderId = "anthropic",
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
                ModelsDevProviderId = "openai",
                // Codex is a coding CLI: hide realtime/voice/image/embedding endpoints,
                // ChatGPT-app aliases, and pre-GPT-5 generations.
                ModelExcludePattern = @"realtime|image|embed|audio|tts|transcribe|deep-research|^chatgpt|^o\d|^gpt-[34]",
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
                ModelsDevProviderId = "google",
                // Gemini CLI only runs gemini chat models, not gemma/embedding/media endpoints.
                ModelExcludePattern = @"^gemma|embedding|image|tts|audio|omni|live|robotics|^learnlm|^aqa",
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
                ModelsDevProviderId = "moonshotai",
                InstallUrl = "https://github.com/MoonshotAI/kimi-cli",
                LoginHint = "Run the CLI once to configure your Moonshot account.",
                Note = "Moonshot Kimi CLI. Adjust the arguments to your installed version.",
            },
            new()
            {
                Id = "cursor",
                Label = "Cursor CLI",
                DefaultExecutable = "cursor-agent",
                DefaultArguments = "-p --trust --approve-mcps {model} {prompt}",
                ModelArgTemplate = "--model {model}",
                DefaultModels = "default",
                ListModelsArgs = "--list-models",
                InstallUrl = "https://cursor.com/cli",
                LoginArgs = "login",
                LoginHint = "Completes Cursor sign-in in your browser.",
                Note = "Cursor CLI (cursor-agent). Each turn is stateless.",
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

        /// <summary>Manual model-list override; empty = auto-fetch via <see cref="ModelCatalogService"/>.</summary>
        public static PlayerPrefsString ModelsSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Models", string.Empty);

        /// <summary>Multiline "KEY=VALUE" pairs injected into the agent process environment.</summary>
        public static PlayerPrefsString EnvironmentSetting(AgentBackendPreset preset)
            => new($"Feeder_MatrixSpace_Backend_{preset.Id}_Env", string.Empty);

        public static List<string> GetModels(AgentBackendPreset preset)
        {
            ModelCatalogService.EnsureLoaded();

            // A manual override wins entirely. Values equal to the old defaults are treated
            // as "never customized" (they were the pre-dynamic-catalog PlayerPrefs default).
            var overrideCsv = ModelsSetting(preset).Value;
            if (!string.IsNullOrWhiteSpace(overrideCsv) && overrideCsv.Trim() != preset.DefaultModels)
                return ParseCsv(overrideCsv);

            var models = new List<string> { "default" };

            var dynamicIds = ModelCatalogService.GetCliListedModels(preset)
                             ?? (preset.ModelsDevProviderId != null
                                 ? ModelCatalogService.GetModelsForProvider(preset.ModelsDevProviderId)
                                 : null);
            if (dynamicIds != null)
            {
                var exclude = preset.ModelExcludePattern.Length > 0
                    ? new System.Text.RegularExpressions.Regex(preset.ModelExcludePattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    : null;
                foreach (var id in dynamicIds)
                {
                    if (models.Count > MaxDropdownModels)
                        break;
                    if (!models.Contains(id) && exclude?.IsMatch(id) != true)
                        models.Add(id);
                }
            }

            // Offline / not-yet-fetched fallback.
            if (models.Count == 1)
                AddCsv(models, preset.DefaultModels);
            return models;
        }

        /// <summary>Dropdown cap; the newest models come first, older ones stay reachable via override.</summary>
        private const int MaxDropdownModels = 20;

        private static List<string> ParseCsv(string csv)
        {
            var models = new List<string>();
            AddCsv(models, csv);
            if (models.Count == 0)
                models.Add("default");
            return models;
        }

        private static void AddCsv(List<string> models, string csv)
        {
            foreach (var raw in csv.Split(','))
            {
                var model = raw.Trim();
                if (model.Length > 0 && !models.Contains(model))
                    models.Add(model);
            }
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
