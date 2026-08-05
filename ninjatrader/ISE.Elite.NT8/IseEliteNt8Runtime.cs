using System;
using ISE.BrokerExecution;
using ISE.NinjaTraderAdapter;
using ISE.NinjaTraderHost;

namespace ISE.Elite.NinjaTrader8;

public sealed class IseEliteNt8Runtime : IDisposable
{
    private readonly IseEliteNt8Options _options;
    private readonly NinjaTraderApiAdapter _api;
    private readonly NinjaTraderHostTransport _host;
    private readonly Sim101SmokeTestController _smokeTest;
    private bool _started;

    public IseEliteNt8Runtime(IseEliteNt8Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _api = new NinjaTraderApiAdapter(options);
        _host = new NinjaTraderHostTransport(_api, options.AccountName);
        Broker = new NinjaTraderExecutionBroker(_host, new NinjaTraderInstrumentMapper());
        _smokeTest = new Sim101SmokeTestController(Broker, options.SmokeTestEnabled, options.InstrumentRoot);

        _api.OrderUpdateReceived += OnPlatformOrderUpdate;
        _api.ExecutionReceived += snapshot => ExecutionReceived?.Invoke(snapshot);
        _api.PositionReceived += snapshot => PositionReceived?.Invoke(snapshot);
        _api.Diagnostic += message => Diagnostic?.Invoke(message);
        _host.BrokerEvent += OnBrokerEvent;
    }

    public IExecutionBroker Broker { get; }
    public bool IsStarted => _started;
    public bool SmokeTestEnabled => _options.SmokeTestEnabled;
    public decimal SmokeTestLimitPrice => _options.SmokeTestLimitPrice;
    public SmokeTestState SmokeTestState => _smokeTest.State;

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
            Diagnostic?.Invoke("ISE Elite NT8 runtime is running on Sim101.");
            Diagnostic?.Invoke(_options.SmokeTestEnabled
                ? $"Sim101 smoke test is enabled but disarmed at buy-limit price {_options.SmokeTestLimitPrice}."
                : "Sim101 smoke test is disabled.");
        }
        catch
        {
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
            "No protective child orders are active.");
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
        _host.BrokerEvent -= OnBrokerEvent;
        Stop();
        _api.Dispose();
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
            _host.HandleOrderUpdate(update);
        }
        catch (Exception exception)
        {
            Diagnostic?.Invoke(
                $"Order update {update.PlatformOrderId} could not be correlated: {exception.Message}");
        }
    }

    private void EnsureStarted()
    {
        if (!_started)
            throw new InvalidOperationException("ISE Elite NT8 runtime is not started.");
    }
}
