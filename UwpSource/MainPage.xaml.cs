using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Gaming.Input;
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
        private DispatcherTimer _controllerTimer;
        private bool _coreWindowHooksAttached;
        private GamepadButtons _lastButtons = GamepadButtons.None;
        private string _currentPage = "home";

        public MainPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AttachKeyboardHooks();
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
                _coreWindowHooksAttached = false;
            }

            _pressedKeys.Clear();
        }

        private void AttachKeyboardHooks()
        {
            if (_coreWindowHooksAttached) return;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
            _coreWindowHooksAttached = true;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            _pressedKeys.Add(args.VirtualKey);
            if (_currentPage == "input")
            {
                KeyText.Text = args.VirtualKey.ToString();
                KeyStateText.Text = "Key state: down  •  Keys held: " + _pressedKeys.Count;
            }

            if (args.VirtualKey == VirtualKey.Escape && _currentPage != "home")
            {
                ShowPanel("home");
                NavHomeButton.Focus(FocusState.Programmatic);
                args.Handled = true;
            }
            else if (args.VirtualKey == VirtualKey.F1)
            {
                ShowPanel("input");
                NavInputButton.Focus(FocusState.Programmatic);
                args.Handled = true;
            }
        }

        private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            _pressedKeys.Remove(args.VirtualKey);
            if (_currentPage == "input")
            {
                KeyText.Text = args.VirtualKey.ToString();
                KeyStateText.Text = "Key state: released  •  Keys held: " + _pressedKeys.Count;
            }
        }

        private void InputSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(InputSurface);
            MouseText.Text = string.Format("X: {0:0}   Y: {1:0}", point.Position.X, point.Position.Y);
            MouseStateText.Text = "Pointer: " + point.PointerDevice.PointerDeviceType;
        }

        private void InputSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(InputSurface);
            MouseStateText.Text = "Pressed: " + point.PointerDevice.PointerDeviceType;
        }

        private void InputSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            MouseStateText.Text = "Pointer button released";
        }

        private void ControllerTimer_Tick(object sender, object e)
        {
            UpdateControllerDisplay();
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

            if (backPressed && _currentPage != "home")
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

        private void NavAbout_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("about");
        }

        private void ShowPanel(string page)
        {
            _currentPage = page;
            HomePanel.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            InputPanel.Visibility = page == "input" ? Visibility.Visible : Visibility.Collapsed;
            AboutPanel.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;

            NavHomeButton.Opacity = page == "home" ? 1.0 : 0.72;
            NavInputButton.Opacity = page == "input" ? 1.0 : 0.72;
            NavAboutButton.Opacity = page == "about" ? 1.0 : 0.72;

            HeaderStatusText.Text =
                page == "home" ? "HOME" :
                page == "input" ? "INPUT DIAGNOSTICS" : "PROJECT INFO";
        }
    }
}
