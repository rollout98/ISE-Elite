using System;
using System.Windows;
using ISE.BrokerExecution;
using ISE.Elite.NinjaTrader8;
using ISE.NinjaTraderHost;
using ISE.PositionManager;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.AddOns
{
    public sealed class IseEliteProtectedFillTestAddOn : AddOnBase
    {
        private NTMenuItem? _controlCenterNewMenu;
        private NTMenuItem? _armMenu;
        private NTMenuItem? _submitMenu;
        private NTMenuItem? _verifyMenu;
        private Window? _controlCenterWindow;
        private IseEliteNt8Runtime? _subscribedRuntime;
        private ProtectedFillTestController? _controller;
        private IseEliteNt8Options? _options;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
                Name = "ISE Elite Protected Fill Test";
            else if (State == State.Terminated)
                UnsubscribeRuntime();
        }

        protected override void OnWindowCreated(Window window)
        {
            var controlCenter = window as NinjaTrader.Gui.ControlCenter;
            if (controlCenter == null || _controlCenterNewMenu != null)
                return;

            _controlCenterNewMenu = controlCenter.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;
            if (_controlCenterNewMenu == null)
                return;

            _controlCenterWindow = window;
            var menuStyle = Application.Current.TryFindResource("MainMenuItem") as Style;

            _armMenu = new NTMenuItem
            {
                Header = "ISE Elite: Arm Protected Fill Test",
                Style = menuStyle
            };
            _submitMenu = new NTMenuItem
            {
                Header = "ISE Elite: Submit 1 MNQ Market Protected Fill",
                Style = menuStyle
            };
            _verifyMenu = new NTMenuItem
            {
                Header = "ISE Elite: Verify Protected Fill Status",
                Style = menuStyle
            };

            _armMenu.Click += OnArm;
            _submitMenu.Click += OnSubmit;
            _verifyMenu.Click += OnVerify;
            _controlCenterNewMenu.Items.Add(_armMenu);
            _controlCenterNewMenu.Items.Add(_submitMenu);
            _controlCenterNewMenu.Items.Add(_verifyMenu);
        }

        protected override void OnWindowDestroyed(Window window)
        {
            if (_controlCenterWindow == null || !ReferenceEquals(window, _controlCenterWindow))
                return;

            RemoveMenuItem(_armMenu, OnArm);
            RemoveMenuItem(_submitMenu, OnSubmit);
            RemoveMenuItem(_verifyMenu, OnVerify);
            _armMenu = null;
            _submitMenu = null;
            _verifyMenu = null;
            _controlCenterNewMenu = null;
            _controlCenterWindow = null;
        }

        private void OnArm(object sender, RoutedEventArgs e)
        {
            if (!TryGetController(out var runtime, out var controller, out var options))
                return;

            var state = runtime!.PositionState;
            var result = MessageBox.Show(
                _controlCenterWindow,
                "ARM ONE PROTECTED MNQ MARKET ENTRY ON SIM101?\n\n" +
                $"Current state: {state.Status}; side={state.ExpectedSide}; quantity={state.ExpectedQuantity}; " +
                $"brokerSigned={state.BrokerSignedQuantity}.\n\n" +
                $"After a fill, ISE must submit a {options!.ProtectiveStopTicks}-tick stop and " +
                $"{options.ProtectiveTargetTicks}-tick target as one OCO pair.\n" +
                "Arming does not submit an order.",
                "ISE Elite — Arm Protected Fill Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                controller!.Arm(
                    ProtectedFillTestController.ConfirmationPhrase,
                    IsFlat(state),
                    runtime.ProtectionEnabled,
                    DateTime.UtcNow);
                WriteOutput("Operator armed the protected-fill test. No order was submitted.");
                MessageBox.Show(_controlCenterWindow,
                    "Protected-fill test armed. No order has been submitted.\n\n" +
                    "Use the separate Submit command only while monitoring Orders, Positions, and Output 1.",
                    "ISE Elite — Protected Fill Armed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void OnSubmit(object sender, RoutedEventArgs e)
        {
            if (!TryGetController(out var runtime, out var controller, out var options))
                return;

            var state = runtime!.PositionState;
            var result = MessageBox.Show(
                _controlCenterWindow,
                "FINAL CONFIRMATION\n\n" +
                "Submit BUY MARKET 1 MNQ to Sim101?\n\n" +
                $"Required protection after fill: stop={options!.ProtectiveStopTicks} ticks; " +
                $"target={options.ProtectiveTargetTicks} ticks; OCO linked.\n\n" +
                "This order is expected to fill. Keep the Emergency Flatten command available.",
                "ISE Elite — Submit Protected Fill",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var submitted = controller!.SubmitMarketBuy(IsFlat(state), DateTime.UtcNow);
                WriteOutput(
                    $"Protected-fill entry submitted: request={submitted.RequestId}; platform={submitted.PlatformOrderId}.");
                MessageBox.Show(_controlCenterWindow,
                    $"Protected-fill entry submitted to Sim101.\n\nPlatform order: {submitted.PlatformOrderId}\n" +
                    "Watch for the entry fill followed by two working OCO protective orders.",
                    "ISE Elite — Protected Fill Submitted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void OnVerify(object sender, RoutedEventArgs e)
        {
            if (!TryGetController(out var runtime, out var controller, out _))
                return;

            try
            {
                var state = runtime!.PositionState;
                if (state.Status == PositionManagerStatus.Protected &&
                    !string.IsNullOrWhiteSpace(state.StopOrderId) &&
                    !string.IsNullOrWhiteSpace(state.TargetOrderId))
                {
                    controller!.MarkProtected(state.StopOrderId!, state.TargetOrderId!, DateTime.UtcNow);
                }
                else if (IsFlat(state))
                {
                    controller!.MarkCompleted(DateTime.UtcNow);
                }

                MessageBox.Show(_controlCenterWindow,
                    $"Test state: {controller!.State}\n\n" +
                    $"Position state: {state.Status}\n" +
                    $"Side: {state.ExpectedSide}\nQuantity: {state.ExpectedQuantity}\n" +
                    $"Broker signed: {state.BrokerSignedQuantity}\n" +
                    $"Stop: {state.StopOrderId ?? "not confirmed"}\n" +
                    $"Target: {state.TargetOrderId ?? "not confirmed"}",
                    "ISE Elite — Protected Fill Status",
                    MessageBoxButton.OK,
                    state.Status == PositionManagerStatus.Protected
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private bool TryGetController(out IseEliteNt8Runtime? runtime,
            out ProtectedFillTestController? controller, out IseEliteNt8Options? options)
        {
            runtime = IseEliteNt8BridgeRegistry.Runtime;
            controller = null;
            options = null;

            if (runtime == null || !runtime.IsStarted)
            {
                MessageBox.Show(_controlCenterWindow,
                    "ISE Elite NT8 runtime is not running. Start or reconnect the main bridge first.",
                    "ISE Elite — Protected Fill Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            try
            {
                options = IseEliteNt8Options.Load(IseEliteNt8Options.DefaultConfigurationPath);
                if (!options.ProtectedFillTestEnabled)
                {
                    MessageBox.Show(_controlCenterWindow,
                        "The protected-fill test is disabled. Keep it disabled until the supervised fill-test window.",
                        "ISE Elite — Protected Fill Disabled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return false;
                }

                if (!ReferenceEquals(_subscribedRuntime, runtime))
                {
                    UnsubscribeRuntime();
                    _subscribedRuntime = runtime;
                    _controller = new ProtectedFillTestController(
                        runtime.Broker, options.ProtectedFillTestEnabled, options.InstrumentRoot);
                    runtime.BrokerEventReceived += OnBrokerEvent;
                    runtime.PositionReceived += OnPositionReceived;
                }

                controller = _controller;
                _options = options;
                return true;
            }
            catch (Exception exception)
            {
                ShowError(exception);
                return false;
            }
        }

        private void OnBrokerEvent(BrokerOrderEvent brokerEvent)
        {
            try
            {
                _controller?.HandleBrokerEvent(brokerEvent);
                if (_controller?.Request != null &&
                    string.Equals(_controller.Request.RequestId, brokerEvent.RequestId, StringComparison.Ordinal))
                    WriteOutput($"Protected-fill broker event: {brokerEvent.State}; filled={brokerEvent.FilledQuantity}; " +
                        $"average={brokerEvent.AverageFillPrice}.");
            }
            catch (Exception exception)
            {
                WriteOutput("Protected-fill broker event failed: " + exception.Message);
            }
        }

        private void OnPositionReceived(NinjaTraderPositionSnapshot position)
        {
            try
            {
                if (_subscribedRuntime == null || _controller == null || _options == null)
                    return;
                if (!string.Equals(position.Instrument, _options.InstrumentFullName,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                var state = _subscribedRuntime.PositionState;
                if (IsFlat(state))
                    _controller.MarkCompleted(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                WriteOutput("Protected-fill position tracking failed: " + exception.Message);
            }
        }

        private void UnsubscribeRuntime()
        {
            if (_subscribedRuntime != null)
            {
                _subscribedRuntime.BrokerEventReceived -= OnBrokerEvent;
                _subscribedRuntime.PositionReceived -= OnPositionReceived;
            }
            _subscribedRuntime = null;
            _controller = null;
            _options = null;
        }

        private static bool IsFlat(PositionManagerSnapshot state) =>
            state.ExpectedQuantity == 0 && state.BrokerSignedQuantity == 0;

        private void ShowError(Exception exception)
        {
            WriteOutput("Protected-fill command failed: " + exception.Message);
            MessageBox.Show(_controlCenterWindow,
                exception.Message,
                "ISE Elite — Protected Fill Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void RemoveMenuItem(NTMenuItem? menuItem, RoutedEventHandler handler)
        {
            if (menuItem == null)
                return;
            menuItem.Click -= handler;
            if (_controlCenterNewMenu != null && _controlCenterNewMenu.Items.Contains(menuItem))
                _controlCenterNewMenu.Items.Remove(menuItem);
        }

        private static void WriteOutput(string message)
        {
            NinjaTrader.Code.Output.Process(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}",
                PrintTo.OutputTab1);
        }
    }
}
