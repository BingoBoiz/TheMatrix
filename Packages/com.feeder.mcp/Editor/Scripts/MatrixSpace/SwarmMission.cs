#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// MatrixSwarm-lite: turns one mission statement into role prompts dispatched to panes
    /// in parallel. Coordination happens over the shared mailbox files — no extra runtime.
    /// </summary>
    public static class SwarmMission
    {
        public sealed class Role
        {
            public string Id = string.Empty;
            public string Label = string.Empty;
            public string Charter = string.Empty;
            public bool DefaultEnabled;
        }

        public static readonly Role[] Roles =
        {
            new()
            {
                Id = "coordinator",
                Label = "Coordinator",
                DefaultEnabled = true,
                Charter =
                    "You are the COORDINATOR. Break the mission into work items, assign each item to one teammate " +
                    "by writing directives into your mailbox file, monitor the other mailbox files for progress, " +
                    "and keep a running mission status summary in the shared memory file. Do not implement anything yourself.",
            },
            new()
            {
                Id = "builder-a",
                Label = "Builder A",
                DefaultEnabled = true,
                Charter =
                    "You are BUILDER A. Implement your share of the mission. Check the coordinator's mailbox file for " +
                    "your assignment first; if there is none yet, pick the most foundational part and claim it in your " +
                    "mailbox file so no one duplicates it. Only modify files you claimed.",
            },
            new()
            {
                Id = "builder-b",
                Label = "Builder B",
                DefaultEnabled = true,
                Charter =
                    "You are BUILDER B. Implement your share of the mission. Check the coordinator's mailbox file for " +
                    "your assignment first; if there is none yet, pick a part not claimed by Builder A and claim it in " +
                    "your mailbox file. Only modify files you claimed.",
            },
            new()
            {
                Id = "reviewer",
                Label = "Reviewer",
                DefaultEnabled = true,
                Charter =
                    "You are the REVIEWER. Do not implement features. Inspect what the builders produced (their mailbox " +
                    "files list what they touched), verify correctness and consistency, and report findings — one " +
                    "mailbox entry per finding with severity and a suggested fix.",
            },
            new()
            {
                Id = "scout",
                Label = "Scout",
                DefaultEnabled = false,
                Charter =
                    "You are the SCOUT. Do not modify anything. Explore the project (scenes, scripts, assets) relevant " +
                    "to the mission and publish a concise map of what exists — key files, entry points, risks — to your " +
                    "mailbox file and the shared memory.",
            },
        };

        public static string BuildPrompt(Role role, string mission, IReadOnlyList<Role> activeRoles, string agentLabel)
        {
            var teammates = new List<string>();
            foreach (var r in activeRoles)
            {
                if (r.Id != role.Id)
                    teammates.Add(r.Label);
            }

            return
                $"[SWARM MISSION — role: {role.Label} — designation: {agentLabel}]\n\n" +
                $"MISSION:\n{mission}\n\n" +
                $"ROLE CHARTER:\n{role.Charter}\n\n" +
                "TEAM PROTOCOL:\n" +
                $"- Active teammates: {string.Join(", ", teammates)} (each runs in its own pane).\n" +
                $"- Mailbox: append your updates to MatrixSpace/mailbox/{role.Id}.md " +
                "(claims, progress, findings, directives). Read the other files in MatrixSpace/mailbox/ before acting.\n" +
                "- Shared memory: MatrixSpace/MEMORY.md holds durable team knowledge — read it first, append what the team must not forget.\n" +
                "- Work within this Unity project. Be concrete and finish your slice; end your turn with a mailbox update.";
        }
    }
}
