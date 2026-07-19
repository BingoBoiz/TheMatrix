#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// One automated installer per agent backend. `custom` has none — there is nothing
    /// generic to install for a user-defined CLI.
    /// </summary>
    public static class AgentSetupCatalog
    {
        private static readonly Dictionary<string, IAgentSetup> _setups = Build();

        private static Dictionary<string, IAgentSetup> Build()
        {
            var all = new IAgentSetup[]
            {
                new ClaudeSetup(),
                new CodexSetup(),
                new GeminiSetup(),
                new KimiSetup(),
                new CursorSetup(),
            };

            var map = new Dictionary<string, IAgentSetup>();
            foreach (var setup in all)
                map[setup.PresetId] = setup;
            return map;
        }

        public static IAgentSetup? Get(string presetId)
            => _setups.TryGetValue(presetId, out var setup) ? setup : null;
    }
}
