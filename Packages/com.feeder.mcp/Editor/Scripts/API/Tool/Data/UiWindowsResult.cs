#nullable enable
using System.ComponentModel;

namespace AIGD
{
    public class UiWindowsResult
    {
        [Description("All EditorWindow instances currently alive in the editor.")]
        public UiWindowInfo[]? Windows { get; set; }

        [Description("Total number of windows returned.")]
        public int Count { get; set; }
    }
}
