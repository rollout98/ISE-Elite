using System;
using System.Windows;
using ISE.Elite.NinjaTrader8;
using ISE.NinjaTraderHost;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.AddOns
{
    public sealed class IseEliteNt8AddOn : AddOnBase
    {
        private NTMenuItem? _controlCenterNewMenu;
        private NTMenuItem? _armSmokeTestMenu;
        private NTMenuItem? _submitSmokeTestMenu;
        private NTMenuItem? _cancelSmokeTestMenu;
        private Window? _controlCenterWindow;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ISE Elite NT8 Bridge";
                return;
            }

            if (State == State.Active)
            {
                StartBridge();
                return;
            }

            if (State == State.Terminated)
                StopBridge();
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

            _armSmokeTestMenu = new NTMenuItem
            {
                Header = "ISE Elite: Arm Sim101 Smoke Test",
                Style = menuStyle
            };
            _submitSmokeTestMenu = new NTMenuItem
            {
                Header = "ISE Elite: Submit 1 MNQ Buy-Limit Smoke Test",
                Style = menuStyle
            };
            _cancelSmokeTestMenu = new NTMenuItem
            {
                Header = "ISE Elite: Cancel Smoke-Test Order",
                Style = menuStyle
            };

            _armSmokeTestMenu.Click += OnArmSmokeTest;
            _submitSmokeTestMenu.Click += OnSubmitSmokeTest;
            _cancelSmokeTestMenu.Click += OnCancelSmokeTest;

            _controlCenterNewMenu.Items.Add(_armSmokeTestMenu);
            _controlCenterNewMenu.Items.Add(_submitSmokeTestMenu);
            _controlCenterNewMenu.Items.Add(_cancelSmokeTestMenu);
        }

        protected override void OnWindowDestroyed(Window window)
        {
            if (_controlCenterWindow == null || !ReferenceEquals(window, _controlCenterWindow))
                return;

            RemoveMenuItem(_armSmokeTestMenu, OnArmSmokeTest);
            RemoveMenuItem(_submitSmokeTestMenu, OnSubmitSmokeTest);
            RemoveMenuItem(_cancelSmokeTestMenu, OnCancelSmokeTest);

            _armSmokeTestMenu = null;
            _submitSmokeTestMenu = null;
            _cancelSmokeTestMenu = null;
            _controlCenterNewMenu = null;
            _controlCenterWindow = null;
        }

        private void OnArmSmokeTest(object sender, RoutedEventArgs e)
        {
            var runtime = IseEliteNt8BridgeRegistry.Runtime;
            if (!TryGetSmokeTestRuntime(runtime))
                return;

            var result = MessageBox.Show(
                _controlCenterWindow,
                "ARM SIM101 SMOKE TEST?\n\n" +
                "This enables one MNQ buy-limit order for this NinjaTrader runtime session only.\n" +
                "It cannot submit to a live account and does not submit an order during arming.",
                "ISE Elite — Arm Sim101 Smoke Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                runtime!.ArmSmokeTest(Sim101SmokeTestController.ConfirmationPhrase);
                WriteOutput("Operator armed the Sim101 smoke test.");
                MessageBox.Show(_controlCenterWindow,
                    "Smoke test armed. No order has been submitted.\n\n" +
                    "Use the separate Submit menu command only after checking the configured limit price.",
                    "ISE Elite — Armed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                ShowSmokeTestError(exception);
            }
        }

        private void OnSubmitSmokeTest(object sender, RoutedEventArgs e)
        {
            var runtime = IseEliteNt8BridgeRegistry.Runtime;
            if (!TryGetSmokeTestRuntime(runtime))
                return;

            var result = MessageBox.Show(
                _controlCenterWindow,
                $"FINAL CONFIRMATION\n\nSubmit BUY LIMIT 1 MNQ to Sim101 at {runtime!.SmokeTestLimitPrice}?\n\n" +
                "This is a real simulated order. No protective stop or target order will be placed. " +
                "Use a non-marketable limit price and cancel it after acceptance.",
                "ISE Elite — Submit Sim101 Smoke Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var submitted = runtime.SubmitSmokeTestBuyLimit();
                WriteOutput(
                    $"Operator submitted smoke-test request {submitted.RequestId} as {submitted.PlatformOrderId}.");
                MessageBox.Show(_controlCenterWindow,
                    $"Smoke-test order submitted to Sim101.\n\nPlatform order: {submitted.PlatformOrderId}\n" +
                    "Confirm it in the Orders tab, then use the ISE Elite cancel command.",
                    "ISE Elite — Order Submitted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                ShowSmokeTestError(exception);
            }
        }

        private void OnCancelSmokeTest(object sender, RoutedEventArgs e)
        {
            var runtime = IseEliteNt8BridgeRegistry.Runtime;
            if (!TryGetSmokeTestRuntime(runtime))
                return;

            var result = MessageBox.Show(
                _controlCenterWindow,
                "Request cancellation of the ISE Elite Sim101 smoke-test order?",
                "ISE Elite — Cancel Smoke Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var cancelled = runtime!.CancelSmokeTest();
                WriteOutput(
                    $"Operator requested cancellation for {cancelled.RequestId} / {cancelled.PlatformOrderId}.");
            }
            catch (Exception exception)
            {
                ShowSmokeTestError(exception);
            }
        }

        private bool TryGetSmokeTestRuntime(IseEliteNt8Runtime? runtime)
        {
            if (runtime == null || !runtime.IsStarted)
            {
                MessageBox.Show(_controlCenterWindow,
                    "ISE Elite NT8 runtime is not running.",
                    "ISE Elite", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!runtime.SmokeTestEnabled)
            {
                MessageBox.Show(_controlCenterWindow,
                    "The smoke test is disabled. Set SmokeTestEnabled=true and configure a non-marketable " +
                    "SmokeTestLimitPrice, rebuild, and restart NinjaTrader.",
                    "ISE Elite — Smoke Test Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private void ShowSmokeTestError(Exception exception)
        {
            WriteOutput("Smoke-test command failed: " + exception.Message);
            MessageBox.Show(_controlCenterWindow,
                exception.Message,
                "ISE Elite — Smoke Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void RemoveMenuItem(NTMenuItem? menuItem, RoutedEventHandler handler)
        {
            if (menuItem == null)
                return;

            menuItem.Click -= handler;
            if (_controlCenterNewMenu != null && _controlCenterNewMenu.Items.Contains(menuItem))
                _controlCenterNewMenu.Items.Remove(menuItem);
        }

        private static void StartBridge()
        {
            if (IseEliteNt8BridgeRegistry.Runtime != null)
                return;

            try
            {
                var options = IseEliteNt8Options.Load(IseEliteNt8Options.DefaultConfigurationPath);
                var runtime = new IseEliteNt8Runtime(options);
                runtime.Diagnostic += WriteOutput;
                runtime.BrokerEventReceived += brokerEvent => WriteOutput(
                    $"Broker event: {brokerEvent.RequestId} {brokerEvent.State} " +
                    $"filled={brokerEvent.FilledQuantity} avg={brokerEvent.AverageFillPrice}");
                runtime.ExecutionReceived += execution => WriteOutput(
                    $"Execution: {execution.ExecutionId} {execution.Instrument} " +
                    $"qty={execution.Quantity} price={execution.Price}");
                runtime.PositionReceived += position => WriteOutput(
                    $"Position: {position.Instrument} {position.MarketPosition} " +
                    $"qty={position.Quantity} avg={position.AveragePrice}");

                runtime.Start();
                IseEliteNt8BridgeRegistry.Runtime = runtime;
                WriteOutput("ISE Elite NT8 Bridge started in Sim101-only mode.");
            }
            catch (Exception exception)
            {
                WriteOutput($"ISE Elite NT8 Bridge did not start: {exception.Message}");
                WriteOutput($"Configuration path: {IseEliteNt8Options.DefaultConfigurationPath}");
            }
        }

        private static void StopBridge()
        {
            var runtime = IseEliteNt8BridgeRegistry.Runtime;
            IseEliteNt8BridgeRegistry.Runtime = null;
            if (runtime == null)
                return;

            try
            {
                runtime.Dispose();
                WriteOutput("ISE Elite NT8 Bridge stopped.");
            }
            catch (Exception exception)
            {
                WriteOutput($"ISE Elite NT8 Bridge shutdown error: {exception.Message}");
            }
        }

        private static void WriteOutput(string message)
        {
            NinjaTrader.Code.Output.Process(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}",
                PrintTo.OutputTab1);
        }
    }
}
