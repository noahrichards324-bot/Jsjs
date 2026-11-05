using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Gaming.Input;
using Windows.Storage;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace HorizonXboxStarter
{
    public sealed partial class MainPage : Page
    {
        private readonly HashSet<VirtualKey> _pressedKeys = new HashSet<VirtualKey>();
        private readonly Dictionary<string, VirtualKey> _keyBindings = new Dictionary<string, VirtualKey>();
        private readonly Dictionary<string, VirtualKey> _defaultKeyBindings = new Dictionary<string, VirtualKey>();
        private readonly Dictionary<string, ComboBox> _keyBindingEditors = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, string> _mouseBindings = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _defaultMouseBindings = new Dictionary<string, string>();
        private readonly Dictionary<string, ComboBox> _mouseBindingEditors = new Dictionary<string, ComboBox>();
        private readonly List<string> _keyBindingOrder = new List<string>();
        private readonly List<string> _mouseBindingOrder = new List<string>();
        private readonly VirtualKey[] _availableKeys = new VirtualKey[]
        {
            VirtualKey.None,
            VirtualKey.A, VirtualKey.B, VirtualKey.C, VirtualKey.D, VirtualKey.E, VirtualKey.F,
            VirtualKey.G, VirtualKey.H, VirtualKey.I, VirtualKey.J, VirtualKey.K, VirtualKey.L,
            VirtualKey.M, VirtualKey.N, VirtualKey.O, VirtualKey.P, VirtualKey.Q, VirtualKey.R,
            VirtualKey.S, VirtualKey.T, VirtualKey.U, VirtualKey.V, VirtualKey.W, VirtualKey.X,
            VirtualKey.Y, VirtualKey.Z,
            VirtualKey.Number0, VirtualKey.Number1, VirtualKey.Number2, VirtualKey.Number3,
            VirtualKey.Number4, VirtualKey.Number5, VirtualKey.Number6, VirtualKey.Number7,
            VirtualKey.Number8, VirtualKey.Number9,
            VirtualKey.Space, VirtualKey.LeftShift, VirtualKey.RightShift,
            VirtualKey.LeftControl, VirtualKey.RightControl, VirtualKey.Tab, VirtualKey.Enter,
            VirtualKey.Escape, VirtualKey.Back, VirtualKey.Up, VirtualKey.Down,
            VirtualKey.Left, VirtualKey.Right,
            VirtualKey.F1, VirtualKey.F2, VirtualKey.F3, VirtualKey.F4, VirtualKey.F5,
            VirtualKey.F6, VirtualKey.F7, VirtualKey.F8, VirtualKey.F9, VirtualKey.F10,
            VirtualKey.F11, VirtualKey.F12
        };
        private DispatcherTimer _controllerTimer;
        private bool _mouseCaptured;
        private bool _hasMousePosition;
        private Windows.Foundation.Point _lastMousePosition;
        private double _mouseSensitivity = 1.0;
        private bool _invertMouseY;
        private bool _coreWindowHooksAttached;
        private GamepadButtons _lastButtons = GamepadButtons.None;
        private bool _nativeBridgeAvailable;
        private bool _gameRunning;
        private IntPtr _enginePanelNative = IntPtr.Zero;
        private bool _engineMouseCaptured;
        private bool _hasEngineMousePosition;
        private Windows.Foundation.Point _engineLastMousePosition;
        private IntPtr _nativeSurfaceHandle = IntPtr.Zero;
        private const uint NativeKeyEvent = 1;
        private const uint NativeMouseButtonEvent = 2;
        private const uint NativeMouseMoveEvent = 3;
        private const uint NativeMouseWheelEvent = 4;
        private const uint NativeGamepadButtonEvent = 5;
        private const uint NativeGamepadAxisEvent = 6;
        private string _currentPage = "home";

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInputEvent
        {
            public uint Kind;
            public uint Code;
            public uint State;
            public float X;
            public float Y;
        }

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_engine_bridge_abi_version",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint NativeAbiVersion();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_input_event_size",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint NativeInputEventSize();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_input_push",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeInputPush(uint kind, uint code, uint state, float x, float y);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_input_pop",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeInputPop(out NativeInputEvent inputEvent);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_input_clear",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeInputClear();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_game_start",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeGameStart(IntPtr panel, uint width, uint height);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_game_input_push",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeGameInputPush(uint kind, uint code, uint state, float x, float y);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_game_stop",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeGameStop();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_game_is_running",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint NativeGameIsRunning();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_surface_create",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NativeSurfaceCreate(IntPtr panel, uint width, uint height);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_surface_last_error",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeSurfaceLastError();

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_surface_resize",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeSurfaceResize(IntPtr host, uint width, uint height);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_surface_present",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeSurfacePresent(IntPtr host);

        [DllImport("horizon_uwp_engine_bridge.dll", EntryPoint = "horizon_uwp_surface_destroy",
            ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeSurfaceDestroy(IntPtr host);

        public MainPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AttachKeyboardHooks();
            EngineSwapChainPanel.CompositionScaleChanged += EngineSwapChainPanel_CompositionScaleChanged;
            InitializeControlSettings();
            InitializeNativeBridge();
            _controllerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _controllerTimer.Tick += ControllerTimer_Tick;
            _controllerTimer.Start();

            ShowPanel("home");
            AppStatusText.Text = "UWP shell is running. Input diagnostics are ready.";
            UpdateControllerDisplay();
            NavHomeButton.Focus(FocusState.Programmatic);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_controllerTimer != null)
            {
                _controllerTimer.Stop();
                _controllerTimer.Tick -= ControllerTimer_Tick;
                _controllerTimer = null;
            }

            if (_coreWindowHooksAttached)
            {
                Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
                Window.Current.CoreWindow.KeyUp -= CoreWindow_KeyUp;
                Window.Current.CoreWindow.Activated -= CoreWindow_Activated;
                _coreWindowHooksAttached = false;
            }
            EngineSwapChainPanel.CompositionScaleChanged -= EngineSwapChainPanel_CompositionScaleChanged;

            _pressedKeys.Clear();
            if (_nativeBridgeAvailable)
            {
                try { NativeInputClear(); } catch { }
            }
            DestroyNativeSurface();
        }

        private void InitializeNativeBridge()
        {
            try
            {
                uint version = NativeAbiVersion();
                if (version != 3)
                {
                    _nativeBridgeAvailable = false;
                    NativeBridgeStatusText.Text = "Native bridge version mismatch. Expected ABI v3.";
                    return;
                }

                uint nativeEventSize = NativeInputEventSize();
                int managedEventSize = Marshal.SizeOf(typeof(NativeInputEvent));
                if (nativeEventSize != managedEventSize || nativeEventSize != 20)
                {
                    _nativeBridgeAvailable = false;
                    NativeBridgeStatusText.Text = "Native input layout mismatch. Rust=" +
                        nativeEventSize + " bytes; app=" + managedEventSize + " bytes.";
                    return;
                }

                NativeInputClear();
                _nativeBridgeAvailable = true;
                NativeBridgeStatusText.Text =
                    "Native bridge ABI v3 ready. Horizon renderer, game host and input APIs are present.";
            }
            catch (DllNotFoundException)
            {
                _nativeBridgeAvailable = false;
                NativeBridgeStatusText.Text = "Native bridge DLL not packaged yet.";
            }
            catch (EntryPointNotFoundException)
            {
                _nativeBridgeAvailable = false;
                NativeBridgeStatusText.Text = "Native bridge DLL is outdated or incomplete.";
            }
            catch (BadImageFormatException)
            {
                _nativeBridgeAvailable = false;
                NativeBridgeStatusText.Text = "Native bridge architecture does not match this app.";
            }
        }

        // Round-trip one diagnostic event through the Rust C ABI. The test panel
        // immediately removes the event again, so it cannot fill the game queue.
        private void TestNativeInputRoundTrip(uint kind, uint code, uint state, float x, float y)
        {
            if (!_nativeBridgeAvailable) return;

            try
            {
                int pushed = NativeInputPush(kind, code, state, x, y);
                if (pushed != 0)
                {
                    NativeBridgeStatusText.Text = "Native input queue rejected a diagnostic event: " + pushed;
                    return;
                }

                NativeInputEvent received;
                int popped = NativeInputPop(out received);
                if (popped != 1 || received.Kind != kind || received.Code != code ||
                    received.State != state || Math.Abs(received.X - x) > 0.001f ||
                    Math.Abs(received.Y - y) > 0.001f)
                {
                    NativeBridgeStatusText.Text = "Native input ABI round-trip check failed.";
                    return;
                }

                NativeBridgeStatusText.Text =
                    "Native input ABI v2: event round-trip OK. Gameplay is not connected yet.";
            }
            catch (Exception ex)
            {
                _nativeBridgeAvailable = false;
                NativeBridgeStatusText.Text = "Native input bridge stopped: " + ex.GetType().Name;
            }
        }

        private void AttachKeyboardHooks()
        {
            if (_coreWindowHooksAttached) return;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
            Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            _coreWindowHooksAttached = true;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            bool wasAlreadyDown = _pressedKeys.Contains(args.VirtualKey);
            _pressedKeys.Add(args.VirtualKey);
            ShowMappedKeyAction(args.VirtualKey);

            if (_currentPage == "engine" && _gameRunning)
            {
                SendGameInput(NativeKeyEvent, (uint)args.VirtualKey, wasAlreadyDown ? 2u : 1u, 0.0f, 0.0f);
                args.Handled = true;
                return;
            }

            if (_currentPage == "input")
            {
                KeyText.Text = args.VirtualKey.ToString();
                KeyStateText.Text = "Key state: down  •  Keys held: " + _pressedKeys.Count;
                TestNativeInputRoundTrip(NativeKeyEvent, (uint)args.VirtualKey, 1, 0.0f, 0.0f);
            }

            // Escape backs out of the shell's test/settings pages. In a future
            // connected game page, the engine receives the mapped Pause action.
            if (args.VirtualKey == VirtualKey.Escape && _currentPage != "home")
            {
                ReleaseMouseCapture();
                ShowPanel("home");
                NavHomeButton.Focus(FocusState.Programmatic);
                args.Handled = true;
                return;
            }

            // Arrow keys navigate the app shell. WASD is reserved for Minecraft movement.
            FocusNavigationDirection? direction = GetFocusDirection(args.VirtualKey);
            if (direction.HasValue && FocusManager.TryMoveFocus(direction.Value))
            {
                args.Handled = true;
            }
        }

        private FocusNavigationDirection? GetFocusDirection(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Left:
                    return FocusNavigationDirection.Left;
                case VirtualKey.Right:
                    return FocusNavigationDirection.Right;
                case VirtualKey.Up:
                    return FocusNavigationDirection.Up;
                case VirtualKey.Down:
                    return FocusNavigationDirection.Down;
                default:
                    return null;
            }
        }

        private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            _pressedKeys.Remove(args.VirtualKey);
            if (_currentPage == "engine" && _gameRunning)
            {
                SendGameInput(NativeKeyEvent, (uint)args.VirtualKey, 0, 0.0f, 0.0f);
                args.Handled = true;
                return;
            }
            if (_currentPage == "input")
            {
                KeyText.Text = args.VirtualKey.ToString();
                KeyStateText.Text = "Key state: released  •  Keys held: " + _pressedKeys.Count;
                TestNativeInputRoundTrip(NativeKeyEvent, (uint)args.VirtualKey, 0, 0.0f, 0.0f);
            }
        }

        private void InputSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(MouseCaptureSurface);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;

            var position = point.Position;
            MouseText.Text = string.Format("X: {0:0}   Y: {1:0}", position.X, position.Y);
            MouseStateText.Text = _mouseCaptured ? "Mouse: captured for relative movement test" : "Mouse: moving";

            if (_hasMousePosition)
            {
                double dx = (position.X - _lastMousePosition.X) * _mouseSensitivity;
                double dy = (position.Y - _lastMousePosition.Y) * _mouseSensitivity;
                if (_invertMouseY) dy = -dy;
                MouseDeltaText.Text = string.Format("Scaled look delta  X: {0:0.00}   Y: {1:0.00}", dx, dy);
                TestNativeInputRoundTrip(NativeMouseMoveEvent, 0, 0, (float)dx, (float)dy);
            }
            else
            {
                MouseDeltaText.Text = "Move again to measure mouse delta.";
                _hasMousePosition = true;
            }

            _lastMousePosition = position;
        }

        private void InputSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(MouseCaptureSurface);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;

            MouseCaptureSurface.CapturePointer(e.Pointer);
            _mouseCaptured = true;
            _hasMousePosition = false;

            string button = GetMouseButtonName(point.Properties.PointerUpdateKind.ToString());
            string actions = GetMouseActions(button);
            MouseStateText.Text = "Pressed: " + (button ?? point.Properties.PointerUpdateKind.ToString());
            uint buttonCode = GetMouseButtonCode(button);
            if (buttonCode != 0)
                TestNativeInputRoundTrip(NativeMouseButtonEvent, buttonCode, 1, 0.0f, 0.0f);
            if (!string.IsNullOrEmpty(actions))
            {
                LastInputActionText.Text = "Mouse " + button + " → " + actions;
            }
            MouseDeltaText.Text = "Mouse captured in the test area. Press Escape to release.";
        }

        private void InputSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(MouseCaptureSurface);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            string button = GetMouseButtonName(point.Properties.PointerUpdateKind.ToString());
            MouseStateText.Text = "Released: " + (button ?? "mouse button");
            uint buttonCode = GetMouseButtonCode(button);
            if (buttonCode != 0)
                TestNativeInputRoundTrip(NativeMouseButtonEvent, buttonCode, 0, 0.0f, 0.0f);
            ReleaseMouseCapture();
        }

        private void InputSurface_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(MouseCaptureSurface);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            int delta = point.Properties.MouseWheelDelta;
            MouseStateText.Text = "Mouse wheel: " + delta;
            LastInputActionText.Text = "Mouse wheel → hotbar scroll";
            TestNativeInputRoundTrip(NativeMouseWheelEvent, 0, 0, delta, 0.0f);
        }

        private void ReleaseMouseCapture()
        {
            if (MouseCaptureSurface != null)
            {
                MouseCaptureSurface.ReleasePointerCaptures();
            }
            _mouseCaptured = false;
            _hasMousePosition = false;
        }

        private string GetMouseButtonName(string updateKind)
        {
            if (updateKind.StartsWith("LeftButton")) return "Left";
            if (updateKind.StartsWith("RightButton")) return "Right";
            if (updateKind.StartsWith("MiddleButton")) return "Middle";
            if (updateKind.StartsWith("XButton1")) return "X1";
            if (updateKind.StartsWith("XButton2")) return "X2";
            return null;
        }

        private uint GetMouseButtonCode(string button)
        {
            switch (button)
            {
                case "Left": return 1;
                case "Right": return 2;
                case "Middle": return 3;
                case "X1": return 4;
                case "X2": return 5;
                default: return 0;
            }
        }

        private string GetMouseActions(string button)
        {
            if (string.IsNullOrEmpty(button)) return null;
            var actions = _mouseBindingOrder
                .Where(action => _mouseBindings.ContainsKey(action) && _mouseBindings[action] == button)
                .ToArray();
            return actions.Length == 0 ? null : string.Join(", ", actions);
        }

        private void ControllerTimer_Tick(object sender, object e)
        {
            UpdateControllerDisplay();
            if (_gameRunning)
            {
                try
                {
                    _gameRunning = NativeGameIsRunning() != 0;
                    if (!_gameRunning)
                    {
                        EngineSurfaceButton.Content = "START HORIZON";
                        EngineSurfaceStatusText.Text =
                            "Horizon stopped. Check the game status and local files if it did not reach the title screen.";
                        _engineMouseCaptured = false;
                        _hasEngineMousePosition = false;
                    }
                }
                catch
                {
                    _gameRunning = false;
                }
            }
        }

        private void UpdateControllerDisplay()
        {
            Gamepad pad = Gamepad.Gamepads.FirstOrDefault();
            if (pad == null)
            {
                ControllerStatusText.Text = "No controller detected. Connect an Xbox controller.";
                ControllerButtonsText.Text = "Buttons: —";
                ControllerAxesText.Text = "Left stick: 0.00, 0.00   Right stick: 0.00, 0.00";
                TriggerText.Text = "Triggers: 0.00 / 0.00";
                _lastButtons = GamepadButtons.None;
                return;
            }

            GamepadReading reading = pad.GetCurrentReading();
            ControllerStatusText.Text = "Controller connected — live input is being read.";

            if (_currentPage == "input")
            {
                foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
                {
                    if (button == GamepadButtons.None) continue;
                    bool wasPressed = (_lastButtons & button) == button;
                    bool isPressed = (reading.Buttons & button) == button;
                    if (wasPressed != isPressed)
                        TestNativeInputRoundTrip(NativeGamepadButtonEvent, (uint)button,
                            isPressed ? 1u : 0u, 0.0f, 0.0f);
                }

                TestNativeInputRoundTrip(NativeGamepadAxisEvent, 0, 0,
                    (float)reading.LeftThumbstickX, (float)reading.LeftThumbstickY);
                TestNativeInputRoundTrip(NativeGamepadAxisEvent, 1, 0,
                    (float)reading.RightThumbstickX, (float)reading.RightThumbstickY);
                TestNativeInputRoundTrip(NativeGamepadAxisEvent, 2, 0,
                    (float)reading.LeftTrigger, (float)reading.RightTrigger);
            }
            else if (_currentPage == "engine" && _gameRunning)
            {
                foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
                {
                    if (button == GamepadButtons.None) continue;
                    bool wasPressed = (_lastButtons & button) == button;
                    bool isPressed = (reading.Buttons & button) == button;
                    if (wasPressed != isPressed)
                        SendGameInput(NativeGamepadButtonEvent, (uint)button,
                            isPressed ? 1u : 0u, 0.0f, 0.0f);
                }

                SendGameInput(NativeGamepadAxisEvent, 0, 0,
                    (float)reading.LeftThumbstickX, (float)reading.LeftThumbstickY);
                SendGameInput(NativeGamepadAxisEvent, 1, 0,
                    (float)reading.RightThumbstickX, (float)reading.RightThumbstickY);
                SendGameInput(NativeGamepadAxisEvent, 2, 0,
                    (float)reading.LeftTrigger, (float)reading.RightTrigger);
            }
            ControllerButtonsText.Text = "Buttons: " +
                (reading.Buttons == GamepadButtons.None ? "None" : reading.Buttons.ToString());
            ControllerAxesText.Text = string.Format(
                "Left stick: {0:0.00}, {1:0.00}   Right stick: {2:0.00}, {3:0.00}",
                reading.LeftThumbstickX, reading.LeftThumbstickY,
                reading.RightThumbstickX, reading.RightThumbstickY);
            TriggerText.Text = string.Format(
                "Triggers: {0:0.00} / {1:0.00}", reading.LeftTrigger, reading.RightTrigger);

            bool backPressed =
                (reading.Buttons & GamepadButtons.B) == GamepadButtons.B &&
                (_lastButtons & GamepadButtons.B) == GamepadButtons.None;

            _lastButtons = reading.Buttons;

            if (backPressed && _currentPage != "home" && !_gameRunning)
            {
                ShowPanel("home");
                NavHomeButton.Focus(FocusState.Programmatic);
            }
        }

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("home");
        }

        private void NavInput_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("input");
        }

        private void NavControls_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("controls");
        }

        private void NavEngine_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("engine");
            EngineSurfaceButton.Focus(FocusState.Programmatic);
        }

        private void NavAbout_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("about");
        }

        private void InitializeControlSettings()
        {
            AddDefaultKeyBinding("Move forward", "MoveForward", VirtualKey.W);
            AddDefaultKeyBinding("Move left", "MoveLeft", VirtualKey.A);
            AddDefaultKeyBinding("Move backward", "MoveBack", VirtualKey.S);
            AddDefaultKeyBinding("Move right", "MoveRight", VirtualKey.D);
            AddDefaultKeyBinding("Jump", "Jump", VirtualKey.Space);
            AddDefaultKeyBinding("Sneak", "Sneak", VirtualKey.LeftShift);
            AddDefaultKeyBinding("Sprint", "Sprint", VirtualKey.LeftControl);
            AddDefaultKeyBinding("Drop item", "Drop", VirtualKey.Q);
            AddDefaultKeyBinding("Inventory", "Inventory", VirtualKey.E);
            AddDefaultKeyBinding("Chat", "Chat", VirtualKey.T);
            AddDefaultKeyBinding("Player list", "PlayerList", VirtualKey.Tab);
            AddDefaultKeyBinding("Swap offhand", "SwapOffhand", VirtualKey.F);
            AddDefaultKeyBinding("Hotbar slot 1", "Hotbar1", VirtualKey.Number1);
            AddDefaultKeyBinding("Hotbar slot 2", "Hotbar2", VirtualKey.Number2);
            AddDefaultKeyBinding("Hotbar slot 3", "Hotbar3", VirtualKey.Number3);
            AddDefaultKeyBinding("Hotbar slot 4", "Hotbar4", VirtualKey.Number4);
            AddDefaultKeyBinding("Hotbar slot 5", "Hotbar5", VirtualKey.Number5);
            AddDefaultKeyBinding("Hotbar slot 6", "Hotbar6", VirtualKey.Number6);
            AddDefaultKeyBinding("Hotbar slot 7", "Hotbar7", VirtualKey.Number7);
            AddDefaultKeyBinding("Hotbar slot 8", "Hotbar8", VirtualKey.Number8);
            AddDefaultKeyBinding("Hotbar slot 9", "Hotbar9", VirtualKey.Number9);
            AddDefaultKeyBinding("Toggle HUD (F1)", "ToggleHud", VirtualKey.F1);
            AddDefaultKeyBinding("Take screenshot (F2)", "Screenshot", VirtualKey.F2);
            AddDefaultKeyBinding("Debug overlay (F3)", "DebugOverlay", VirtualKey.F3);
            AddDefaultKeyBinding("Spectator shader toggle (F4)", "SpectatorEffects", VirtualKey.F4);
            AddDefaultKeyBinding("Change perspective (F5)", "Perspective", VirtualKey.F5);
            AddDefaultKeyBinding("Debug options (F6)", "DebugOptions", VirtualKey.F6);
            AddDefaultKeyBinding("Custom function key F7", "CustomF7", VirtualKey.F7);
            AddDefaultKeyBinding("Custom function key F8", "CustomF8", VirtualKey.F8);
            AddDefaultKeyBinding("Custom function key F9", "CustomF9", VirtualKey.F9);
            AddDefaultKeyBinding("Custom function key F10", "CustomF10", VirtualKey.F10);
            AddDefaultKeyBinding("Fullscreen (F11)", "Fullscreen", VirtualKey.F11);
            AddDefaultKeyBinding("Custom function key F12", "CustomF12", VirtualKey.F12);

            AddDefaultMouseBinding("Attack / break", "Attack", "Left");
            AddDefaultMouseBinding("Use / place", "Use", "Right");
            AddDefaultMouseBinding("Pick block", "PickBlock", "Middle");

            var settings = ApplicationData.Current.LocalSettings.Values;
            foreach (string action in _keyBindingOrder)
            {
                object value;
                if (settings.TryGetValue("Key_" + action, out value) && value is string)
                {
                    VirtualKey parsed;
                    if (Enum.TryParse((string)value, out parsed))
                    {
                        _keyBindings[action] = parsed;
                    }
                }
            }

            foreach (string action in _mouseBindingOrder)
            {
                object value;
                if (settings.TryGetValue("Mouse_" + action, out value) && value is string)
                {
                    string saved = (string)value;
                    if (new[] { "Left", "Right", "Middle", "X1", "X2", "None" }.Contains(saved))
                    {
                        _mouseBindings[action] = saved;
                    }
                }
            }

            object sensitivity;
            if (settings.TryGetValue("MouseSensitivity", out sensitivity) && sensitivity is double)
            {
                MouseSensitivitySlider.Value = (double)sensitivity;
            }
            _mouseSensitivity = MouseSensitivitySlider.Value / 100.0;
            MouseSensitivityValueText.Text = MouseSensitivitySlider.Value.ToString("0") + "%";

            object invert;
            if (settings.TryGetValue("InvertMouseY", out invert) && invert is bool)
            {
                InvertMouseYCheckBox.IsChecked = (bool)invert;
            }
            _invertMouseY = InvertMouseYCheckBox.IsChecked == true;

            BuildMouseBindingEditors();
            BuildKeyBindingEditors();
        }

        private void AddDefaultKeyBinding(string label, string action, VirtualKey key)
        {
            _keyBindingOrder.Add(action);
            _keyBindings[action] = key;
            _defaultKeyBindings[action] = key;
        }

        private void AddDefaultMouseBinding(string label, string action, string button)
        {
            _mouseBindingOrder.Add(action);
            _mouseBindings[action] = button;
            _defaultMouseBindings[action] = button;
        }

        private void BuildKeyBindingEditors()
        {
            KeyBindingsPanel.Children.Clear();
            _keyBindingEditors.Clear();

            foreach (string action in _keyBindingOrder)
            {
                string label = GetBindingLabel(action);
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });

                var title = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["MainTextBrush"],
                    FontSize = 15
                };
                var editor = new ComboBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MinWidth = 170,
                    Height = 40
                };

                foreach (VirtualKey key in _availableKeys)
                {
                    editor.Items.Add(key == VirtualKey.None ? "Unbound" : key.ToString());
                }
                editor.SelectedItem = _keyBindings[action] == VirtualKey.None ? "Unbound" : _keyBindings[action].ToString();
                editor.SelectionChanged += (sender, args) =>
                {
                    if (editor.SelectedItem == null) return;
                    string selected = editor.SelectedItem.ToString();
                    VirtualKey parsed = VirtualKey.None;
                    if (selected != "Unbound" && !Enum.TryParse(selected, out parsed)) return;
                    _keyBindings[action] = parsed;
                    ApplicationData.Current.LocalSettings.Values["Key_" + action] = parsed.ToString();
                };

                Grid.SetColumn(editor, 1);
                row.Children.Add(title);
                row.Children.Add(editor);
                KeyBindingsPanel.Children.Add(row);
                _keyBindingEditors[action] = editor;
            }
        }

        private string GetBindingLabel(string action)
        {
            switch (action)
            {
                case "MoveForward": return "Move forward";
                case "MoveBack": return "Move backward";
                case "MoveLeft": return "Move left";
                case "MoveRight": return "Move right";
                case "Jump": return "Jump";
                case "Sneak": return "Sneak";
                case "Sprint": return "Sprint";
                case "Drop": return "Drop item";
                case "Inventory": return "Inventory";
                case "Chat": return "Chat";
                case "PlayerList": return "Player list";
                case "SwapOffhand": return "Swap offhand";
                case "ToggleHud": return "Toggle HUD (F1)";
                case "Screenshot": return "Screenshot (F2)";
                case "DebugOverlay": return "Debug overlay (F3)";
                case "SpectatorEffects": return "Spectator effects (F4)";
                case "Perspective": return "Change perspective (F5)";
                case "DebugOptions": return "Debug options (F6)";
                case "Fullscreen": return "Fullscreen (F11)";
                case "CustomF7": return "Custom function key F7";
                case "CustomF8": return "Custom function key F8";
                case "CustomF9": return "Custom function key F9";
                case "CustomF10": return "Custom function key F10";
                case "CustomF12": return "Custom function key F12";
                default:
                    if (action.StartsWith("Hotbar")) return "Hotbar slot " + action.Substring(6);
                    return action;
            }
        }

        private void BuildMouseBindingEditors()
        {
            MouseBindingsPanel.Children.Clear();
            _mouseBindingEditors.Clear();

            foreach (string action in _mouseBindingOrder)
            {
                string label = action == "Attack" ? "Attack / break button" :
                    action == "Use" ? "Use / place button" : "Pick block button";
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                var title = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["MainTextBrush"],
                    FontSize = 14
                };
                var editor = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 130 };
                foreach (string button in new[] { "Left", "Right", "Middle", "X1", "X2", "None" })
                {
                    editor.Items.Add(button);
                }
                editor.SelectedItem = _mouseBindings[action];
                editor.SelectionChanged += (sender, args) =>
                {
                    if (editor.SelectedItem == null) return;
                    _mouseBindings[action] = editor.SelectedItem.ToString();
                    ApplicationData.Current.LocalSettings.Values["Mouse_" + action] = _mouseBindings[action];
                };
                Grid.SetColumn(editor, 1);
                row.Children.Add(title);
                row.Children.Add(editor);
                MouseBindingsPanel.Children.Add(row);
                _mouseBindingEditors[action] = editor;
            }
        }

        private void ShowMappedKeyAction(VirtualKey key)
        {
            if (key == VirtualKey.None) return;
            string[] actions = _keyBindingOrder
                .Where(action => _keyBindings.ContainsKey(action) && _keyBindings[action] == key)
                .Select(GetBindingLabel)
                .ToArray();
            if (actions.Length > 0)
            {
                LastInputActionText.Text = key + " → " + string.Join(", ", actions);
            }
        }

        private void MouseSensitivitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _mouseSensitivity = e.NewValue / 100.0;
            if (MouseSensitivityValueText != null)
            {
                MouseSensitivityValueText.Text = e.NewValue.ToString("0") + "%";
            }
            ApplicationData.Current.LocalSettings.Values["MouseSensitivity"] = e.NewValue;
        }

        private void InvertMouseYCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _invertMouseY = InvertMouseYCheckBox.IsChecked == true;
            ApplicationData.Current.LocalSettings.Values["InvertMouseY"] = _invertMouseY;
        }

        private void ResetBindings_Click(object sender, RoutedEventArgs e)
        {
            foreach (string action in _keyBindingOrder)
            {
                _keyBindings[action] = _defaultKeyBindings[action];
                ApplicationData.Current.LocalSettings.Values.Remove("Key_" + action);
            }
            foreach (string action in _mouseBindingOrder)
            {
                _mouseBindings[action] = _defaultMouseBindings[action];
                ApplicationData.Current.LocalSettings.Values.Remove("Mouse_" + action);
            }

            ApplicationData.Current.LocalSettings.Values.Remove("MouseSensitivity");
            ApplicationData.Current.LocalSettings.Values.Remove("InvertMouseY");
            MouseSensitivitySlider.Value = 100;
            InvertMouseYCheckBox.IsChecked = false;
            LastInputActionText.Text = "Controls reset to Horizon-style defaults.";
            BuildMouseBindingEditors();
            BuildKeyBindingEditors();
        }


        private void SendGameInput(uint kind, uint code, uint state, float x, float y)
        {
            if (!_nativeBridgeAvailable || !_gameRunning) return;
            try
            {
                int result = NativeGameInputPush(kind, code, state, x, y);
                if (result != 0 && result != -3)
                    EngineSurfaceStatusText.Text = "Game input queue returned " + result + ".";
            }
            catch (Exception ex)
            {
                EngineSurfaceStatusText.Text = "Game input failed: " + ex.GetType().Name;
            }
        }

        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            if (!_gameRunning) return;
            uint focused = args.WindowActivationState == CoreWindowActivationState.Deactivated ? 0u : 1u;
            SendGameInput(7, 0, focused, 0.0f, 0.0f);
            if (focused == 0)
            {
                _engineMouseCaptured = false;
                _hasEngineMousePosition = false;
            }
        }

        private void EngineSwapChainPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_gameRunning) return;
            var point = e.GetCurrentPoint(EngineSwapChainPanel);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            string button = GetMouseButtonName(point.Properties.PointerUpdateKind.ToString());
            uint code = GetMouseButtonCode(button);
            if (code != 0) SendGameInput(NativeMouseButtonEvent, code, 1, 0.0f, 0.0f);
            EngineSwapChainPanel.CapturePointer(e.Pointer);
            _engineMouseCaptured = true;
            _engineLastMousePosition = point.Position;
            _hasEngineMousePosition = true;
            e.Handled = true;
        }

        private void EngineSwapChainPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_gameRunning) return;
            var point = e.GetCurrentPoint(EngineSwapChainPanel);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            string button = GetMouseButtonName(point.Properties.PointerUpdateKind.ToString());
            uint code = GetMouseButtonCode(button);
            if (code != 0) SendGameInput(NativeMouseButtonEvent, code, 0, 0.0f, 0.0f);
            EngineSwapChainPanel.ReleasePointerCaptures();
            _engineMouseCaptured = false;
            _hasEngineMousePosition = false;
            e.Handled = true;
        }

        private void EngineSwapChainPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_gameRunning || !_engineMouseCaptured) return;
            var point = e.GetCurrentPoint(EngineSwapChainPanel);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            if (_hasEngineMousePosition)
            {
                double dx = (point.Position.X - _engineLastMousePosition.X) * _mouseSensitivity;
                double dy = (point.Position.Y - _engineLastMousePosition.Y) * _mouseSensitivity;
                if (_invertMouseY) dy = -dy;
                SendGameInput(NativeMouseMoveEvent, 0, 0, (float)dx, (float)dy);
            }
            _engineLastMousePosition = point.Position;
            _hasEngineMousePosition = true;
            e.Handled = true;
        }

        private void EngineSwapChainPanel_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (!_gameRunning) return;
            var point = e.GetCurrentPoint(EngineSwapChainPanel);
            if (point.PointerDevice.PointerDeviceType.ToString() != "Mouse") return;
            float delta = point.Properties.MouseWheelDelta / 120.0f;
            SendGameInput(NativeMouseWheelEvent, 0, 0, 0.0f, delta);
            e.Handled = true;
        }

        private void EngineSurfaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_nativeBridgeAvailable)
            {
                EngineSurfaceStatusText.Text = "The native Horizon bridge is not available in this package.";
                return;
            }

            if (_gameRunning)
            {
                NativeGameStop();
                EngineSurfaceStatusText.Text = "Stopping Horizon and saving the current world...";
                EngineSurfaceButton.Content = "STOPPING...";
                return;
            }

            IntPtr unknown = IntPtr.Zero;
            IntPtr panelNative = IntPtr.Zero;
            try
            {
                try
                {
                    System.IO.Directory.SetCurrentDirectory(ApplicationData.Current.LocalFolder.Path);
                }
                catch
                {
                    // The host still uses the app's current working directory if this
                    // platform build does not permit changing it.
                }

                unknown = Marshal.GetIUnknownForObject(EngineSwapChainPanel);
                Guid panelInterfaceId = new Guid("63AAD0B8-7C24-40FF-85A8-640D944CC325");
                int queryResult = Marshal.QueryInterface(unknown, ref panelInterfaceId, out panelNative);
                if (queryResult < 0 || panelNative == IntPtr.Zero)
                {
                    EngineSurfaceStatusText.Text =
                        "Could not obtain ISwapChainPanelNative. HRESULT: 0x" + queryResult.ToString("X8");
                    return;
                }

                uint width = GetSurfacePixelWidth();
                uint height = GetSurfacePixelHeight();
                int startResult = NativeGameStart(panelNative, width, height);
                if (startResult != 0)
                {
                    EngineSurfaceStatusText.Text = "Horizon could not start. Native host error: " + startResult;
                    return;
                }

                // Keep the QueryInterface reference alive while the Rust render
                // thread creates its wgpu surface from the panel.
                _enginePanelNative = panelNative;
                panelNative = IntPtr.Zero;
                _gameRunning = true;
                EngineSurfaceButton.Content = "STOP HORIZON";
                EngineSurfaceStatusText.Text =
                    "Horizon is starting. It needs valid Minecraft Java resources in this app's local storage.";
                ShowPanel("engine");
            }
            catch (Exception ex)
            {
                EngineSurfaceStatusText.Text =
                    "Horizon startup failed: " + ex.GetType().Name + " — " + ex.Message;
            }
            finally
            {
                if (panelNative != IntPtr.Zero) Marshal.Release(panelNative);
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            }
        }

        private uint GetSurfacePixelWidth()
        {
            double pixels = EngineSwapChainPanel.ActualWidth * EngineSwapChainPanel.CompositionScaleX;
            return (uint)Math.Max(1.0, Math.Ceiling(pixels));
        }

        private uint GetSurfacePixelHeight()
        {
            double pixels = EngineSwapChainPanel.ActualHeight * EngineSwapChainPanel.CompositionScaleY;
            return (uint)Math.Max(1.0, Math.Ceiling(pixels));
        }

        private void ResizeNativeSurface()
        {
            if (_gameRunning)
                SendGameInput(8, 0, 0, GetSurfacePixelWidth(), GetSurfacePixelHeight());

            if (_nativeSurfaceHandle == IntPtr.Zero) return;
            int result = NativeSurfaceResize(
                _nativeSurfaceHandle, GetSurfacePixelWidth(), GetSurfacePixelHeight());
            if (result != 0)
                EngineSurfaceStatusText.Text = "Swap-chain resize failed with bridge error " + result + ".";
        }

        private void EngineSwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeNativeSurface();
        }

        private void EngineSwapChainPanel_CompositionScaleChanged(SwapChainPanel sender, object args)
        {
            ResizeNativeSurface();
        }

        private void DestroyNativeSurface()
        {
            if (_gameRunning)
            {
                try { NativeGameStop(); } catch { }
                _gameRunning = false;
            }
            if (_nativeSurfaceHandle == IntPtr.Zero) return;
            try { NativeSurfaceDestroy(_nativeSurfaceHandle); } catch { }
            _nativeSurfaceHandle = IntPtr.Zero;
        }

        private void ShowPanel(string page)
        {
            _currentPage = page;
            HomePanel.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            InputPanel.Visibility = page == "input" ? Visibility.Visible : Visibility.Collapsed;
            AboutPanel.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;
            ControlsPanel.Visibility = page == "controls" ? Visibility.Visible : Visibility.Collapsed;
            EnginePanel.Visibility = page == "engine" ? Visibility.Visible : Visibility.Collapsed;

            NavHomeButton.Opacity = page == "home" ? 1.0 : 0.72;
            NavInputButton.Opacity = page == "input" ? 1.0 : 0.72;
            NavControlsButton.Opacity = page == "controls" ? 1.0 : 0.72;
            NavEngineButton.Opacity = page == "engine" ? 1.0 : 0.72;
            NavAboutButton.Opacity = page == "about" ? 1.0 : 0.72;

            HeaderStatusText.Text =
                page == "home" ? "HOME" :
                page == "input" ? "INPUT DIAGNOSTICS" :
                page == "controls" ? "CONTROLS" :
                page == "engine" ? "DX12 SURFACE TEST" : "PROJECT INFO";
        }
    }
}
