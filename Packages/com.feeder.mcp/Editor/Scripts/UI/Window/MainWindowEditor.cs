#nullable enable
using System;
using Feeder.MCP.Runtime.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    public partial class MainWindowEditor : McpWindowBase
    {
        readonly CompositeDisposable _disposables = new();

        Button? _btnConnect;
        Button? _btnAuthorize;
        Action? _startAuthorizeAction;
        VisualElement? _timelinePointUnity;
        AlertPanel? _connectionAuthAlert;
        AlertPanel? _connectionConnectAlert;

        protected override string WindowTitle => "AI Connector";
        protected override string[] WindowUxmlPaths => _windowUxmlPaths;
        protected override string[] WindowUssPaths => _windowUssPaths;

        public static MainWindowEditor ShowWindow()
        {
            var window = GetWindow<MainWindowEditor>("AI Connector");
            window.SetupWindowWithIcon();
            window.Focus();

            return window;
        }
        public static void ShowWindowVoid() => ShowWindow();

        public void Invalidate()
        {
            InvalidateAndReloadAgentUI();
            CreateGUI();
        }
        void OnValidate() => UnityMcpPluginEditor.Instance.Validate();

        private void SaveChanges(string message)
        {
            if (UnityMcpPlugin.IsLogEnabled(LogLevel.Info))
                Debug.Log(message);

            saveChangesMessage = message;

            base.SaveChanges();
            UnityMcpPluginEditor.Instance.Save();
        }

        private void OnChanged(UnityMcpPlugin.UnityConnectionConfig data) => Repaint();

        protected override void OnEnable()
        {
            base.OnEnable();
            _disposables.Add(UnityMcpPluginEditor.SubscribeOnChanged(OnChanged));
        }
        private void OnDisable()
        {
            _disposables.Clear();
            _authRejectedSubscription.Dispose();
        }

        internal static (bool needsAuth, bool hasToken, bool isCloud) ComputeCloudAuthState(ConnectionMode mode, string? token)
        {
            var isCloud = mode == ConnectionMode.Cloud;
            var hasToken = !string.IsNullOrEmpty(token);
            var needsAuth = isCloud && !hasToken;
            return (needsAuth, hasToken, isCloud);
        }

        private void UpdateCloudAuthState()
        {
            var (needsAuth, hasToken, isCloud) = ComputeCloudAuthState(UnityMcpPluginEditor.ConnectionMode, UnityMcpPluginEditor.CloudToken);

            if (_timelinePointUnity != null)
            {
                _timelinePointUnity.SetEnabled(!needsAuth);
                _timelinePointUnity.tooltip = needsAuth
                    ? "Cloud token is required. Press the Authorize button to authenticate."
                    : "";
            }
            if (_btnConnect != null)
            {
                if (needsAuth)
                {
                    _btnConnect.text = ServerButtonText_Connect;
                    _btnConnect.EnableInClassList("btn-primary", false);
                    _btnConnect.EnableInClassList("btn-secondary", true);
                }
                else if (isCloud && hasToken
                    && _btnConnect.text == ServerButtonText_Connect)
                {
                    _btnConnect.EnableInClassList("btn-primary", true);
                    _btnConnect.EnableInClassList("btn-secondary", false);
                }
            }
            if (_btnAuthorize != null)
            {
                _btnAuthorize.EnableInClassList("btn-primary", !hasToken);
            }
            _connectionAuthAlert?.SetVisible(needsAuth);

            // Show connect alert when authorized in Cloud mode but not connected and not trying
            if (_connectionConnectAlert != null)
            {
                var connectionState = UnityMcpPluginEditor.ConnectionState.CurrentValue;
                var keepConnected = UnityMcpPluginEditor.KeepConnected;
                var isDisconnected = connectionState == HubConnectionState.Disconnected;
                var needsConnect = isCloud && hasToken && isDisconnected && !keepConnected;
                _connectionConnectAlert.SetVisible(needsConnect);
            }
        }

        private static void UnityBuildAndConnect()
        {
            UnityMcpPluginEditor.Instance.BuildMcpPluginIfNeeded();
            UnityMcpPluginEditor.Instance.AddUnityLogCollectorIfNeeded(() => new BufferedFileLogStorage());
            UnityMcpPluginEditor.ConnectIfNeeded();
        }

        /// <summary>
        /// Disconnects, disposes the current MCP plugin, rebuilds it (picking up the new Host/Token
        /// from the changed ConnectionMode), and reconnects if KeepConnected is enabled.
        /// Called when switching between Local and Cloud modes.
        /// </summary>
        private static void ReconnectAfterModeSwitch()
        {
            if (UnityMcpPluginEditor.Instance.HasMcpPluginInstance)
            {
                UnityMcpPluginEditor.Instance.DisposeMcpPluginInstance();
            }
            UnityBuildAndConnect();
        }
    }
}