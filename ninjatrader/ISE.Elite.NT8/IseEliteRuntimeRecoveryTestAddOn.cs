using System;
using System.Windows;
using ISE.Elite.NinjaTrader8;
using ISE.NinjaTraderHost;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.AddOns
{
    public sealed class IseEliteRuntimeRecoveryTestAddOn : AddOnBase
    {
        private NTMenuItem? _controlCenterNewMenu;
        private NTMenuItem? _armMenu;
        private NTMenuItem? _restartMenu;
        private Window? _controlCenterWindow;
        private readonly RuntimeRecoveryTestController _controller = new RuntimeRecoveryTestController();

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
                Name = "ISE Elite Runtime Recovery Test";
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
                Header = "ISE Elite: Arm Runtime Recovery Test",
                Style = menuStyle
            };
            _restartMenu = new NTMenuItem
            {
                Header = "ISE Elite: Restart ISE Runtime for Recovery Test",
                Style = menuStyle
            };

            _armMenu.Click += OnArm;
            _restartMenu.Click += OnRestart;
            _controlCenterNewMenu.Items.Add(_armMenu);
            _controlCenterNewMenu.Items.Add(_restartMenu);
        }

        protected override void OnWindowDestroyed(Window window)
        {
            if (_controlCenterWindow == null || !ReferenceEquals(window, _controlCenterWindow))
                return;

            RemoveMenuItem(_armMenu, OnArm);
            RemoveMenuItem(_restartMenu, OnRestart);
            _armMenu = null;
            _restartMenu = null;
            _controlCenterNewMenu = null;
            _controlCenterWindow = null;
        }

        private void OnArm(object sender, RoutedEventArgs e)
        {
            if (!TryGetRuntime(out var runtime))
                return;

            var state = runtime!.PositionState;
            var result = MessageBox.Show(
                _controlCenterWindow,
                "ARM THE ISE RUNTIME RECOVERY TEST?\n\n" +
                "This command does not restart ISE and does not submit an order.\n\n" +
                $"Position: {state.Status}; side={state.ExpectedSide}; quantity={state.ExpectedQuantity}; " +
                $"brokerSigned={state.BrokerSignedQuantity}; average={state.AveragePrice}.\n" +
                $"Stop ID: {state.StopOrderId ?? "missing"}\n" +
                $"Target ID: {state.TargetOrderId ?? "missing"}\n\n" +
                "Arming is allowed only for a fully protected Sim101 position.",
                "ISE Elite — Arm Runtime Recovery Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _controller.Arm(state);
                WriteOutput(
                    $"Runtime recovery test armed: side={state.ExpectedSide}; quantity={state.ExpectedQuantity}; " +
                    $"average={state.AveragePrice}; stop={state.StopOrderId}; target={state.TargetOrderId}.");
                MessageBox.Show(_controlCenterWindow,
                    "Runtime recovery test armed. No restart has occurred.\n\n" +
                    "Keep Orders, Positions, and Output 1 visible, then use the separate Restart command.",
                    "ISE Elite — Recovery Test Armed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void OnRestart(object sender, RoutedEventArgs e)
        {
            if (!TryGetRuntime(out var runtime))
                return;

            var current = runtime!.PositionState;
            var expected = _controller.Expectation;
            var result = MessageBox.Show(
                _controlCenterWindow,
                "RESTART ONLY THE ISE RUNTIME?\n\n" +
                "NinjaTrader will remain open. The Sim101 position and broker-held protective orders must remain active.\n\n" +
                $"Current stop: {current.StopOrderId ?? "missing"}\n" +
                $"Current target: {current.TargetOrderId ?? "missing"}\n" +
                $"Armed stop: {expected?.StopOrderId ?? "not armed"}\n" +
                $"Armed target: {expected?.TargetOrderId ?? "not armed"}\n\n" +
                "The test passes only if the restarted runtime recovers the same position and the same order IDs.",
                "ISE Elite — Restart Runtime Recovery Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _controller.BeginRestart(current);

                if (!IseEliteNt8BridgeStartup.TryRestartForRecovery(
                        WriteOutput, out var restartedRuntime, out var failure))
                {
                    _controller.Fail("Runtime restart failed: " +
                                     (string.IsNullOrWhiteSpace(failure) ? "No detail was captured." : failure));
                    ShowFailure(_controller.LastMessage);
                    return;
                }

                var recovered = restartedRuntime!.PositionState;
                var passed = _controller.ValidateRecovered(recovered);
                WriteOutput(
                    $"Runtime recovery validation: result={(passed ? "PASS" : "FAIL")}; " +
                    $"status={recovered.Status}; side={recovered.ExpectedSide}; quantity={recovered.ExpectedQuantity}; " +
                    $"brokerSigned={recovered.BrokerSignedQuantity}; average={recovered.AveragePrice}; " +
                    $"stop={recovered.StopOrderId ?? "missing"}; target={recovered.TargetOrderId ?? "missing"}. " +
                    _controller.LastMessage);

                if (!passed)
                {
                    ShowFailure(_controller.LastMessage);
                    return;
                }

                MessageBox.Show(_controlCenterWindow,
                    "RUNTIME RECOVERY PASSED\n\n" +
                    $"Position: {recovered.ExpectedSide} {recovered.ExpectedQuantity} at {recovered.AveragePrice}\n" +
                    $"Recovered stop: {recovered.StopOrderId}\n" +
                    $"Recovered target: {recovered.TargetOrderId}\n\n" +
                    "The original protective order IDs were retained; no replacement OCO pair was detected.",
                    "ISE Elite — Runtime Recovery Passed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                _controller.Fail(exception.Message);
                ShowError(exception.Message);
            }
        }

        private bool TryGetRuntime(out IseEliteNt8Runtime? runtime)
        {
            runtime = IseEliteNt8BridgeRegistry.Runtime;
            string? failure = null;
            if (runtime == null || !runtime.IsStarted)
            {
                WriteOutput("Runtime recovery command found no active runtime; attempting a safe Sim101 startup retry.");
                IseEliteNt8BridgeStartup.TryStart(WriteOutput, out failure);
                runtime = IseEliteNt8BridgeRegistry.Runtime;
            }

            if (runtime != null && runtime.IsStarted)
                return true;

            ShowError(string.IsNullOrWhiteSpace(failure)
                ? "ISE Elite NT8 runtime is not running."
                : failure!);
            return false;
        }

        private void ShowFailure(string detail)
        {
            WriteOutput("RUNTIME RECOVERY TEST FAILED: " + detail);
            MessageBox.Show(_controlCenterWindow,
                "RUNTIME RECOVERY FAILED\n\n" + detail + "\n\n" +
                "Do not submit another entry. Inspect Orders and Positions immediately. " +
                "Use ISE Elite Emergency Flatten if the open position or protection is inconsistent.",
                "ISE Elite — Runtime Recovery Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void ShowError(string detail)
        {
            WriteOutput("Runtime recovery command failed: " + detail);
            MessageBox.Show(_controlCenterWindow,
                detail,
                "ISE Elite — Runtime Recovery Error",
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
