using System;
using ISE.BrokerExecution;
using ISE.NinjaTraderAdapter;
using ISE.NinjaTraderHost;

namespace ISE.Elite.NinjaTrader8;

public sealed class IseEliteNt8Runtime : IDisposable
{
    private readonly NinjaTraderApiAdapter _api;
    private readonly NinjaTraderHostTransport _host;
    private bool _started;

    public IseEliteNt8Runtime(IseEliteNt8Options options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _api = new NinjaTraderApiAdapter(options);
        _host = new NinjaTraderHostTransport(_api, options.AccountName);
        Broker = new NinjaTraderExecutionBroker(_host, new NinjaTraderInstrumentMapper());

        _api.OrderUpdateReceived += OnPlatformOrderUpdate;
        _api.ExecutionReceived += snapshot => ExecutionReceived?.Invoke(snapshot);
        _api.PositionReceived += snapshot => PositionReceived?.Invoke(snapshot);
        _api.Diagnostic += message => Diagnostic?.Invoke(message);
        _host.BrokerEvent += brokerEvent => BrokerEventReceived?.Invoke(brokerEvent);
    }

    public IExecutionBroker Broker { get; }
    public bool IsStarted => _started;

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
        }
        catch
        {
            _api.Stop();
            throw;
        }
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
        Stop();
        _api.Dispose();
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
}
