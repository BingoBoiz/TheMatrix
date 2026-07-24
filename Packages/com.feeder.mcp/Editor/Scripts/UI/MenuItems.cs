#nullable enable
#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Feeder.MCP.Editor.Utils;
using UnityEditor;
using UnityEngine;
using static Feeder.McpPlugin.Common.Consts.MCP.Server;

namespace Feeder.MCP.Editor.UI
{
    public static class MenuItems
    {
        [MenuItem("Tools/Feeder/Matrix AI Connector", priority = -18)]
        public static void ShowWindow() => MainWindowEditor.ShowWindow();

        [MenuItem("Tools/Feeder/Matrix Space", priority = -17)]
        public static void ShowMatrixSpace() => MatrixSpaceWindow.ShowWindow();

        [MenuItem("Tools/Feeder/Inspect UI/Matrix Space", priority = -16)]
        public static void InspectMatrixSpace() => OpenUiDebuggerFor(MatrixSpaceWindow.ShowWindow());

        [MenuItem("Tools/Feeder/Inspect UI/Matrix AI Connector", priority = -15)]
        public static void InspectMainWindow() => OpenUiDebuggerFor(MainWindowEditor.ShowWindow());

        [MenuItem("Tools/Feeder/Inspect UI/UI Toolkit Debugger", priority = -14)]
        public static void OpenUiToolkitDebugger() => OpenUiDebuggerFor(null);

        [MenuItem("Tools/Feeder/Inspect UI/IMGUI Debugger", priority = -13)]
        public static void OpenImguiDebugger()
        {
            // Unity's built-in IMGUI Debugger: inspect any IMGUI EditorWindow instruction-by-
            // instruction, each with a stack trace pointing to the exact source location.
            if (EditorApplication.ExecuteMenuItem("Window/Analysis/IMGUI Debugger"))
                return;

            // Fallback for Unity versions where the menu path differs: open the internal
            // UnityEditor.GUIViewDebuggerWindow directly via reflection.
            var windowType = FindEditorType("UnityEditor.GUIViewDebuggerWindow");
            if (windowType != null)
            {
                EditorWindow.GetWindow(windowType).Show();
                return;
            }

            NotificationPopupWindow.Show(
                windowTitle: "Error",
                title: "IMGUI Debugger Not Found",
                message: "Unity could not open 'Window/Analysis/IMGUI Debugger'. Open it manually from the Window menu.",
                width: 350,
                minWidth: 350,
                height: 200,
                minHeight: 200);
        }

        /// <summary>
        /// Opens Unity's built-in UI Toolkit Debugger (DevTools-style inspector for Editor UI).
        /// When <paramref name="window"/> is provided, the debugger is targeted directly at it
        /// via the internal <c>UIElementsDebugger.OpenAndInspectWindow</c> API (reflection);
        /// otherwise it just opens the debugger for manual window selection.
        /// </summary>
        static void OpenUiDebuggerFor(EditorWindow? window)
        {
            if (window != null)
            {
                window.Focus();

                var debuggerType = FindEditorType("UnityEditor.UIElements.Debugger.UIElementsDebugger");
                var method = debuggerType?.GetMethod(
                    "OpenAndInspectWindow",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(EditorWindow) },
                    modifiers: null);

                if (method != null)
                {
                    method.Invoke(null, new object[] { window });
                    return;
                }
                // Fall through to the generic open if the internal API is unavailable.
            }

            if (!EditorApplication.ExecuteMenuItem("Window/UI Toolkit/Debugger"))
            {
                NotificationPopupWindow.Show(
                    windowTitle: "Error",
                    title: "UI Toolkit Debugger Not Found",
                    message: "Unity could not open 'Window/UI Toolkit/Debugger'. Open it manually from the Window menu.",
                    width: 350,
                    minWidth: 350,
                    height: 200,
                    minHeight: 200);
            }
        }

        static System.Type? FindEditorType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                    return type;
            }
            return null;
        }

        [MenuItem("Tools/Feeder/MCP/Server/Reinstall Binaries", priority = 1000)]
        public static Task ReinstallServer() => McpServerManager.InstallServerBinaryIfNeeded(force: true);

        [MenuItem("Tools/Feeder/MCP/Server/Delete Binaries", priority = 1001)]
        public static void DeleteServer()
        {
            var result = McpServerManager.DeleteBinaryFolderIfExists();
            if (result)
            {
                NotificationPopupWindow.Show(
                    windowTitle: "Success",
                    title: "MCP Server Binaries Deleted",
                    message: "The MCP server binaries were successfully deleted. You can reinstall them from the Tools menu.",
                    width: 350,
                    minWidth: 350,
                    height: 200,
                    minHeight: 200);
            }
            else
            {
                NotificationPopupWindow.Show(
                    windowTitle: "Error",
                    title: "MCP Server Binaries Not Found",
                    message: "No MCP server binaries were found to delete. They may have already been deleted or were never installed.",
                    width: 350,
                    minWidth: 350,
                    height: 200,
                    minHeight: 200);
            }
        }

        [MenuItem("Tools/Feeder/MCP/Server/Open Logs", priority = 1002)]
        public static void OpenServerLogs() => OpenFile(McpServerManager.ExecutableFolderPath + "/logs/server-log.txt");

        [MenuItem("Tools/Feeder/MCP/Server/Open Log Errors", priority = 1003)]
        public static void OpenServerLogErrors() => OpenFile(McpServerManager.ExecutableFolderPath + "/logs/server-log-error.txt");

        [MenuItem("Tools/Feeder/MCP/Server/Launch MCP Inspector", priority = 1004)]
        public static void LaunchMcpInspector()
        {
            if (UnityMcpPluginEditor.TransportMethod != TransportMethod.streamableHttp)
            {
                NotificationPopupWindow.Show(
                    windowTitle: "Error",
                    title: "HTTP Transport required",
                    message: "The MCP Inspector can only be launched when the transport method is set to HTTP. Please change the transport method in the plugin settings and try again.",
                    width: 350,
                    minWidth: 350,
                    height: 200,
                    minHeight: 200);
                return;
            }

            // Run command in a terminal window: npx @modelcontextprotocol/inspector http://localhost:8080 --transport http
            var npxArgs = $"-y @modelcontextprotocol/inspector {UnityMcpPluginEditor.Host} --transport http";
            Debug.Log($"Launching MCP Inspector with command: npx {npxArgs}");

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "npx",
                Arguments = npxArgs,
                UseShellExecute = true,
                CreateNoWindow = false,
            };

            try
            {
                System.Diagnostics.Process.Start(processInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                var command = $"{processInfo.FileName} {processInfo.Arguments}";
                NotificationPopupWindow.Show(
                    windowTitle: "Launch Failed",
                    title: "Unable to start MCP Inspector",
                    message:
                        "The MCP Inspector could not be started from Unity.\n\n" +
                        "This usually means that Node.js (and npx) is not installed, or 'npx' is not available on your PATH.\n\n" +
                        "Prerequisites:\n" +
                        " - Install Node.js (which includes npx)\n" +
                        " - Ensure 'npx' is available from your terminal/command prompt\n\n" +
                        "You can try running the following command manually in a terminal:\n" +
                        command + "\n\n" +
                        "System error:\n" +
                        ex.Message,
                    width: 450,
                    minWidth: 450,
                    height: 460,
                    minHeight: 460);
            }
            catch (System.Exception ex)
            {
                var command = $"{processInfo.FileName} {processInfo.Arguments}";
                NotificationPopupWindow.Show(
                    windowTitle: "Launch Failed",
                    title: "Unexpected error starting MCP Inspector",
                    message:
                        "An unexpected error occurred while trying to start the MCP Inspector.\n\n" +
                        "You can try running the following command manually in a terminal:\n" +
                        command + "\n\n" +
                        "Error details:\n" +
                        ex.Message,
                    width: 450,
                    minWidth: 450,
                    height: 460,
                    minHeight: 460);
            }
        }

        [MenuItem("Tools/Feeder/MCP/Debug/Serialization Check", priority = 2002)]
        public static void ShowSerializationCheck() => SerializationCheckWindow.ShowWindow();

        [MenuItem("Tools/Feeder/MCP/Reset Config", priority = 2020)]
        public static void ResetConfig()
        {
            UnityMcpPluginEditor.ResetConfig();
            // Reload Domain to ensure all changes are picked up.
            EditorUtility.RequestScriptReload();
        }

        static void OpenFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"File not found: {path}");
                return;
            }
            Application.OpenURL(path);
        }
    }
}
#endif