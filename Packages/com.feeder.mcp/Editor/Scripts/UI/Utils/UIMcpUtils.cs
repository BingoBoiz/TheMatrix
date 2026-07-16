#nullable enable

namespace Feeder.MCP.Editor.UI
{
    public static class UIMcpUtils
    {
        /// <summary>
        /// Formats a token count as a human-readable string with K suffix for thousands.
        /// </summary>
        public static string FormatTokenCount(int tokens)
        {
            if (tokens >= 1000)
            {
                var thousands = tokens / 1000.0;
                return $"{thousands:0.#}K";
            }
            return tokens.ToString();
        }
    }
}
