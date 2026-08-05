using System;
using ISE.BrokerExecution;
using ISE.NinjaTraderAdapter;
using ISE.NinjaTraderHost;
using ISE.PositionManager;

namespace ISE.Elite.NinjaTrader8;

public sealed class IseEliteNt8Runtime : IDisposable
{
    private readonly IseEliteNt8Options _options;
    private readonly NinjaTraderApiAdapter _api;
    private readonly NinjaTraderHostTransport _host;
    private readonly Sim101SmokeTestController _smokeTest;
    private readonly AuthoritativePositionManager _positionManager;
    private readonly ProtectiveOrderCoordinator _protection;
    private readonly object _positionSync = new object();

    private bool _started;
    private string? _stopOrderId;
    private string? _targetOrderId;
    private bool _protectiveExitInProgress;
    private bool _emergencyFlattenIssued;

    public IseEliteNt8Runtime(IseEliteNt8Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _api = new NinjaTraderApiAdapter(options);
        _host = new NinjaTraderHostTransport(_api, options.AccountName);
        Broker = new NinjaTraderExecutionBroker(_host, new NinjaTraderInstrumentMapper());
        _smokeTest = new Sim101SmokeTestController(Broker, options.SmokeTestEnabled, options.InstrumentRoot);
        _positionManager = new AuthoritativePositionManager(options.AccountName, options.InstrumentFullName);
        _protection = new ProtectiveOrderCoordinator(_positionManager,
            time => "ISE-OCO-" + time.ToString("yyyyMMddHHmmssfff"));

        _api.OrderUpdateReceived += OnPlatformOrderUpdate;
        _api.ExecutionReceived += OnExecutionReceived;
        _api.PositionReceived += OnPositionReceived;
        _api.Diagnostic += OnDiagnostic;
        _host.BrokerEvent += OnBrokerEvent;
    }

    public IExecutionBroker Broker { get; }
    public bool IsStarted => _started;
    public bool SmokeTestEnabled => _options.SmokeTestEnabled;
    public decimal SmokeTestLimitPrice => _options.SmokeTestLimitPrice;
    public SmokeTestState SmokeTestState => _smokeTest.State;
    public bool ProtectionEnabled => _options.ProtectionEnabled;
    public PositionManagerSnapshot PositionState
    {
        get
        {
            lock (_positionSync)
                return _positionManager.Current;
        }
    }

    public event Action<BrokerOrderEvent>? BrokerEventReceived;
    public event Action<NinjaTraderExecutionSnapshot>? ExecutionReceived;
    public event Action<NinjaTraderPositionSnapshot>? PositionReceived;
    public event Action<string>? Diagnostic;

    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("ISE Elite NT8 runtime is already started.");

        _api.Start();
        try
        {
            _host.Start();
            _started = true;
            RecoverPositionState();
            Diagnostic?.Invoke("ISE Elite NT8 runtime is running on Sim101.");
            Diagnostic?.Invoke(_options.SmokeTestEnabled
                ? $"Sim101 smoke test is enabled but disarmed at buy-limit price {_options.SmokeTestLimitPrice}."
                : "Sim101 smoke test is disabled.");
            Diagnostic?.Invoke(_options.ProtectionEnabled
                ? $"Position protection is enabled: stop={_options.ProtectiveStopTicks} ticks; " +
                  $"target={_options.ProtectiveTargetTicks} ticks."
                : "Position protection is disabled.");
        }
        catch
        {
            _started = false;
            _api.Stop();
            throw;
        }
    }

    public void ArmSmokeTest(string confirmationPhrase)
    {
        EnsureStarted();
        _smokeTest.Arm(confirmationPhrase, DateTime.UtcNow);
        Diagnostic?.Invoke("Sim101 smoke test armed for this runtime session.");
    }

    public BrokerOrderEvent SubmitSmokeTestBuyLimit()
    {
        EnsureStarted();
        var submitted = _smokeTest.SubmitBuyLimit(_options.SmokeTestLimitPrice, DateTime.UtcNow);
        Diagnostic?.Invoke(
            $"Smoke-test submission sent: request={submitted.RequestId}; platform={submitted.PlatformOrderId}. " +
            (_options.ProtectionEnabled
                ? "A fill will be reconciled before protective OCO submission."
                : "Protective child orders are disabled."));
        return submitted;
    }

    public BrokerOrderEvent CancelSmokeTest()
    {
        EnsureStarted();
        var cancelled = _smokeTest.Cancel(DateTime.UtcNow);
        Diagnostic?.Invoke(
            $"Smoke-test cancellation sent: request={cancelled.RequestId}; platform={cancelled.PlatformOrderId}.");
        return cancelled;
    }

    public void EmergencyFlatten(string reason)
    {
        EnsureStarted();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An emergency flatten reason is required.", nameof(reason));

        lock (_positionSync)
        {
            if (_positionManager.Current.ExpectedQuantity == 0 &&
                _positionManager.Current.BrokerSignedQuantity == 0)
                throw new InvalidOperationException("Sim101 is already flat for the configured instrument.");

            _protection.CreateEmergencyFlatten(reason, DateTime.UtcNow);
            _emergencyFlattenIssued = true;
        }

        _api.FlattenConfiguredInstrument();
        Diagnostic?.Invoke("Emergency flatten routed to NinjaTrader: " + reason);
    }

    public void Stop()
    {
        if (!_started)
        {
            _api.Stop();
            return;
        }

        _host.Stop();
        _api.Stop();
        _started = false;
        Diagnostic?.Invoke("ISE Elite NT8 runtime stopped.");
    }

    public void Dispose()
    {
        _api.OrderUpdateReceived -= OnPlatformOrderUpdate;
        _api.ExecutionReceived -= OnExecutionReceived;
        _api.PositionReceived -= OnPositionReceived;
        _api.Diagnostic -= OnDiagnostic;
        _host.BrokerEvent -= OnBrokerEvent;
        Stop();
        _api.Dispose();
    }

    private void RecoverPositionState()
    {
        var platformPosition = _api.GetCurrentPositionSnapshot();
        var broker = MapPosition(platformPosition);
        var workingOrders = _api.GetWorkingProtectiveOrders();

        lock (_positionSync)
        {
            var recovered = _protection.Recover(broker, workingOrders);
            _stopOrderId = recovered.StopOrderId;
            _targetOrderId = recovered.TargetOrderId;
            _protectiveExitInProgress = false;
            _emergencyFlattenIssued = false;
            Diagnostic?.Invoke(
                $"Position recovery: status={recovered.Status}; side={recovered.ExpectedSide}; " +
                $"quantity={recovered.ExpectedQuantity}; average={recovered.AveragePrice}.");
        }

        EnsureProtectiveOrders();
    }

    private void OnExecutionReceived(NinjaTraderExecutionSnapshot execution)
    {
        try
        {
            if (string.Equals(execution.Instrument, _options.InstrumentFullName,
                    StringComparison.OrdinalIgnoreCase))
            {
                lock (_positionSync)
                {
                    var result = _positionManager.ApplyFill(new ExecutionFill(
                        execution.ExecutionId,
                        execution.PlatformOrderId,
                        _options.AccountName,
                        execution.Instrument,
                        MapFillAction(execution.OrderAction),
                        execution.Quantity,
                        execution.Price,
                        execution.OccurredAt));

                    Diagnostic?.Invoke(
                        $"Position fill applied: action={execution.OrderAction}; quantity={execution.Quantity}; " +
                        $"price={execution.Price}; expected={result.ExpectedSide} {result.ExpectedQuantity}.");

                    if (result.ExpectedQuantity == 0)
                    {
                        _stopOrderId = null;
                        _targetOrderId = null;
                        _protectiveExitInProgress = false;
                        _emergencyFlattenIssued = false;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke("Execution reconciliation failed: " + exception.Message);
        }
        finally
        {
            ExecutionReceived?.Invoke(execution);
        }
    }

    private void OnPositionReceived(NinjaTraderPositionSnapshot position)
    {
        try
        {
            if (string.Equals(position.Instrument, _options.InstrumentFullName,
                    StringComparison.OrdinalIgnoreCase))
            {
                PositionManagerSnapshot reconciled;
                lock (_positionSync)
                {
                    reconciled = _positionManager.Reconcile(MapPosition(position));
                    Diagnostic?.Invoke(
                        $"Broker position reconciled: status={reconciled.Status}; side={reconciled.ExpectedSide}; " +
                        $"quantity={reconciled.ExpectedQuantity}; brokerSigned={reconciled.BrokerSignedQuantity}.");
                }

                if (reconciled.Status == PositionManagerStatus.ReconciliationRequired)
                {
                    Diagnostic?.Invoke(
                        "Position mismatch detected. New entries and protective planning remain blocked until broker truth is reconciled.");
                }
                else
                {
                    EnsureProtectiveOrders();
                }
            }
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke("Position reconciliation failed: " + exception.Message);
        }
        finally
        {
            PositionReceived?.Invoke(position);
        }
    }

    private void EnsureProtectiveOrders()
    {
        if (!_started || !_options.ProtectionEnabled)
            return;

        ProtectiveOrderPair? pair = null;
        lock (_positionSync)
        {
            var current = _positionManager.Current;
            if (current.Status != PositionManagerStatus.OpenUnprotected || current.ExpectedQuantity == 0)
                return;

            var tickSize = _api.GetConfiguredTickSize();
            var stopDistance = tickSize * _options.ProtectiveStopTicks;
            var targetDistance = tickSize * _options.ProtectiveTargetTicks;
            var stopPrice = current.ExpectedSide == PositionSide.Long
                ? current.AveragePrice - stopDistance
                : current.AveragePrice + stopDistance;
            var targetPrice = current.ExpectedSide == PositionSide.Long
                ? current.AveragePrice + targetDistance
                : current.AveragePrice - targetDistance;

            pair = _protection.Plan(stopPrice, targetPrice, DateTime.UtcNow);
        }

        try
        {
            var submission = _api.SubmitProtectivePair(pair!);
            lock (_positionSync)
            {
                _stopOrderId = submission.StopOrderId;
                _targetOrderId = submission.TargetOrderId;
                _protectiveExitInProgress = false;
            }

            Diagnostic?.Invoke(
                $"Protective pair submitted: OCO={submission.OcoGroup}; stop={submission.StopOrderId}; " +
                $"target={submission.TargetOrderId}.");
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke("Protective order submission failed: " + exception.Message);
            if (_options.EmergencyFlattenOnProtectionFailure)
                TryEmergencyFlatten("Protective order submission failed.");
        }
    }

    private void OnBrokerEvent(BrokerOrderEvent brokerEvent)
    {
        _smokeTest.HandleBrokerEvent(brokerEvent);
        Diagnostic?.Invoke(
            $"Broker event: request={brokerEvent.RequestId}; state={brokerEvent.State}; " +
            $"filled={brokerEvent.FilledQuantity}; average={brokerEvent.AverageFillPrice}; {brokerEvent.Message}");
        BrokerEventReceived?.Invoke(brokerEvent);
    }

    private void OnPlatformOrderUpdate(PlatformOrderUpdate update)
    {
        try
        {
            ProtectiveOrderKind? kind = null;
            lock (_positionSync)
            {
                if (string.Equals(update.PlatformOrderId, _stopOrderId, StringComparison.OrdinalIgnoreCase))
                    kind = ProtectiveOrderKind.Stop;
                else if (string.Equals(update.PlatformOrderId, _targetOrderId, StringComparison.OrdinalIgnoreCase))
                    kind = ProtectiveOrderKind.Target;
            }

            if (kind.HasValue)
            {
                HandleProtectiveOrderUpdate(kind.Value, update);
                return;
            }

            _host.HandleOrderUpdate(update);
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke(
                $"Order update {update.PlatformOrderId} could not be processed: {exception.Message}");
        }
    }

    private void HandleProtectiveOrderUpdate(ProtectiveOrderKind kind, PlatformOrderUpdate update)
    {
        var state = MapProtectiveState(update.State);
        ProtectiveOrderTransition transition;

        lock (_positionSync)
        {
            if (state == ProtectivePlatformOrderState.Accepted ||
                state == ProtectivePlatformOrderState.Working)
            {
                _protection.RecordSubmitted(kind, update.PlatformOrderId, update.OccurredAt);
            }

            if (state == ProtectivePlatformOrderState.Filled)
                _protectiveExitInProgress = true;

            transition = _protection.HandleTransition(kind, state, update.PlatformOrderId);

            if (state == ProtectivePlatformOrderState.Cancelled && _protectiveExitInProgress)
            {
                transition = new ProtectiveOrderTransition(kind, state, update.PlatformOrderId,
                    null, false, "OCO sibling cancellation confirmed after protective exit.");
            }
        }

        Diagnostic?.Invoke(
            $"Protective order event: kind={kind}; state={state}; id={update.PlatformOrderId}; " +
            transition.Message);

        if (state == ProtectivePlatformOrderState.Filled &&
            !string.IsNullOrWhiteSpace(transition.SiblingOrderId))
        {
            try
            {
                _api.Cancel(_options.AccountName, transition.SiblingOrderId!);
            }
            catch (Exception exception)
            {
                Diagnostic?.Invoke("Sibling cancellation follow-up: " + exception.Message);
            }
        }

        if (transition.EmergencyFlattenRequired && _options.EmergencyFlattenOnProtectionFailure)
            TryEmergencyFlatten(transition.Message);
    }

    private void TryEmergencyFlatten(string reason)
    {
        lock (_positionSync)
        {
            if (_emergencyFlattenIssued)
                return;
            if (_positionManager.Current.ExpectedQuantity == 0 &&
                _positionManager.Current.BrokerSignedQuantity == 0)
                return;
            _emergencyFlattenIssued = true;
        }

        try
        {
            EmergencyFlatten(reason);
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke("Emergency flatten failed: " + exception.Message);
        }
    }

    private void OnDiagnostic(string message) => Diagnostic?.Invoke(message);

    private BrokerPositionSnapshot MapPosition(NinjaTraderPositionSnapshot position)
    {
        var side = position.MarketPosition switch
        {
            "Long" => PositionSide.Long,
            "Short" => PositionSide.Short,
            _ => PositionSide.Flat
        };
        var quantity = side == PositionSide.Flat ? 0 : Math.Abs(position.Quantity);
        var averagePrice = quantity == 0 ? 0m : position.AveragePrice;
        return new BrokerPositionSnapshot(_options.AccountName, _options.InstrumentFullName,
            side, quantity, averagePrice, position.OccurredAt);
    }

    private static FillAction MapFillAction(string action) => action switch
    {
        "Buy" => FillAction.Buy,
        "Sell" => FillAction.Sell,
        "SellShort" => FillAction.SellShort,
        "BuyToCover" => FillAction.BuyToCover,
        _ => throw new InvalidOperationException("Unsupported NinjaTrader order action: " + action)
    };

    private static ProtectivePlatformOrderState MapProtectiveState(PlatformOrderState state) => state switch
    {
        PlatformOrderState.Submitted => ProtectivePlatformOrderState.Submitted,
        PlatformOrderState.Accepted => ProtectivePlatformOrderState.Accepted,
        PlatformOrderState.PartiallyFilled => ProtectivePlatformOrderState.PartiallyFilled,
        PlatformOrderState.Filled => ProtectivePlatformOrderState.Filled,
        PlatformOrderState.Cancelled => ProtectivePlatformOrderState.Cancelled,
        PlatformOrderState.Rejected => ProtectivePlatformOrderState.Rejected,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private void EnsureStarted()
    {
        if (!_started)
            throw new InvalidOperationException("ISE Elite NT8 runtime is not started.");
    }
}
