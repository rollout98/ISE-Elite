using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BrokerExecution;

namespace ISE.NinjaTraderHost;

public enum HostState { Stopped, Starting, Running, Stopping, Faulted }
public enum PlatformOrderState { Submitted, Accepted, PartiallyFilled, Filled, Rejected, Cancelled }

public sealed class PlatformOrderUpdate
{
    public PlatformOrderUpdate(string platformOrderId, PlatformOrderState state, int filledQuantity,
        decimal averageFillPrice, string message, DateTime occurredAt)
    {
        PlatformOrderId = string.IsNullOrWhiteSpace(platformOrderId)
            ? throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId))
            : platformOrderId;
        if (filledQuantity < 0) throw new ArgumentOutOfRangeException(nameof(filledQuantity));
        PlatformOrderId = platformOrderId;
        State = state;
        FilledQuantity = filledQuantity;
        AverageFillPrice = averageFillPrice;
        Message = message ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public string PlatformOrderId { get; }
    public PlatformOrderState State { get; }
    public int FilledQuantity { get; }
    public decimal AverageFillPrice { get; }
    public string Message { get; }
    public DateTime OccurredAt { get; }
}

public interface INinjaTraderApi
{
    bool IsConnected { get; }
    IReadOnlyCollection<string> AccountNames { get; }
    string Submit(string accountName, BrokerOrderCommand command);
    void Cancel(string accountName, string platformOrderId);
}

public sealed class NinjaTraderHostTransport : INinjaTraderTransport
{
    private readonly INinjaTraderApi _api;
    private readonly string _accountName;
    private readonly Dictionary<string, string> _requestByPlatform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public NinjaTraderHostTransport(INinjaTraderApi api, string accountName = "Sim101")
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _accountName = string.IsNullOrWhiteSpace(accountName)
            ? throw new ArgumentException("Account name is required.", nameof(accountName))
            : accountName;
    }

    public HostState State { get; private set; } = HostState.Stopped;
    public string AccountName => _accountName;
    public event Action<BrokerOrderEvent>? BrokerEvent;

    public void Start()
    {
        if (State != HostState.Stopped) throw new InvalidOperationException("Host is already started.");
        State = HostState.Starting;
        if (!_api.IsConnected) { State = HostState.Faulted; throw new InvalidOperationException("NinjaTrader is not connected."); }
        if (!_api.AccountNames.Contains(_accountName)) { State = HostState.Faulted; throw new InvalidOperationException("Configured account is unavailable."); }
        State = HostState.Running;
    }

    public void Stop()
    {
        if (State == HostState.Stopped) return;
        State = HostState.Stopping;
        State = HostState.Stopped;
    }

    public string Submit(BrokerOrderCommand command)
    {
        EnsureRunning();
        if (command == null) throw new ArgumentNullException(nameof(command));
        var platformOrderId = _api.Submit(_accountName, command);
        if (string.IsNullOrWhiteSpace(platformOrderId)) throw new InvalidOperationException("NinjaTrader returned no order ID.");
        if (_requestByPlatform.ContainsKey(platformOrderId)) throw new InvalidOperationException("Platform order ID is already correlated.");
        _requestByPlatform.Add(platformOrderId, command.RequestId);
        return platformOrderId;
    }

    public void Cancel(string platformOrderId)
    {
        EnsureRunning();
        if (!_requestByPlatform.ContainsKey(platformOrderId)) throw new KeyNotFoundException("Unknown platform order ID.");
        _api.Cancel(_accountName, platformOrderId);
    }

    public BrokerOrderEvent HandleOrderUpdate(PlatformOrderUpdate update)
    {
        EnsureRunning();
        if (update == null) throw new ArgumentNullException(nameof(update));
        if (!_requestByPlatform.TryGetValue(update.PlatformOrderId, out var requestId))
            throw new KeyNotFoundException("Order update is not correlated to an ISE request.");

        var brokerEvent = new BrokerOrderEvent(requestId, update.PlatformOrderId, Map(update.State),
            update.FilledQuantity, update.AverageFillPrice, update.Message, update.OccurredAt);
        BrokerEvent?.Invoke(brokerEvent);
        return brokerEvent;
    }

    private void EnsureRunning()
    {
        if (State != HostState.Running) throw new InvalidOperationException("NinjaTrader host is not running.");
    }

    private static BrokerOrderState Map(PlatformOrderState state) => state switch
    {
        PlatformOrderState.Submitted => BrokerOrderState.Submitted,
        PlatformOrderState.Accepted => BrokerOrderState.Accepted,
        PlatformOrderState.PartiallyFilled => BrokerOrderState.PartiallyFilled,
        PlatformOrderState.Filled => BrokerOrderState.Filled,
        PlatformOrderState.Rejected => BrokerOrderState.Rejected,
        PlatformOrderState.Cancelled => BrokerOrderState.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
