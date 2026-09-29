#nullable enable
using System;

namespace Feeder.MCP.Editor.UI
{
    public enum BridgePhase
    {
        Offline,
        Linking,
        Online,
        Fault,
    }

    public static class BridgeStatus
    {
        public static BridgePhase Resolve(bool keepConnected, bool connected, bool connecting, McpServerStatus server, string? installError)
        {
            if (!string.IsNullOrEmpty(installError))
                return BridgePhase.Fault;
            if (connected)
                return BridgePhase.Online;
            if (keepConnected || connecting || server == McpServerStatus.Installing || server == McpServerStatus.Starting)
                return BridgePhase.Linking;
            return BridgePhase.Offline;
        }

        public static string Word(BridgePhase phase) => phase switch
        {
            BridgePhase.Offline => "OFFLINE",
            BridgePhase.Linking => "LINKING",
            BridgePhase.Online => "ONLINE",
            BridgePhase.Fault => "FAULT",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };

        public static float RainEnergy(BridgePhase phase) => phase switch
        {
            BridgePhase.Offline => 0.08f,
            BridgePhase.Linking => 0.5f,
            BridgePhase.Online => 1f,
            BridgePhase.Fault => 0.2f,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };
    }
}
