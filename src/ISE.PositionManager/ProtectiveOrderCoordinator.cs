using System;
using ISE.ExecutionCoordinator;

namespace ISE.PositionManager;

public enum ProtectivePlatformOrderState
{
    Submitted,
    Accepted,
    Working,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}

public sealed class ProtectiveOrderInstruction
{
    public ProtectiveOrderInstruction(ProtectiveOrderKind kind, string accountName, string instrument,
        ExecutionSide side, int quantity, decimal price, string ocoGroup, string signalName)
    {
        if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentException("Account name is required.", nameof(accountName));
        if (string.IsNullOrWhiteSpace(instrument)) throw new ArgumentException("Instrument is required.", nameof(instrument));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (price <= 0m) throw new ArgumentOutOfRangeException(nameof(price));
        if (string.IsNullOrWhiteSpace(ocoGroup)) throw new ArgumentException("OCO group is required.", nameof(ocoGroup));
        if (string.IsNullOrWhiteSpace(signalName)) throw new ArgumentException("Signal name is required.", nameof(signalName));

        Kind = kind;
        AccountName = accountName;
        Instrument = instrument;
        Side = side;
        Quantity = quantity;
        Price = price;
        OcoGroup = ocoGroup;
        SignalName = signalName;
    }

    public ProtectiveOrderKind Kind { get; }
    public string AccountName { get; }
    public string Instrument { get; }
    public ExecutionSide Side { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public string OcoGroup { get; }
    public string SignalName { get; }
}

public sealed class ProtectiveOrderPair
{
    public ProtectiveOrderPair(string ocoGroup, ProtectiveOrderInstruction stop,
        ProtectiveOrderInstruction target)
    {
        OcoGroup = string.IsNullOrWhiteSpace(ocoGroup)
            ? throw new ArgumentException("OCO group is required.", nameof(ocoGroup))
            : ocoGroup;
        Stop = stop ?? throw new ArgumentNullException(nameof(stop));
        Target = target ?? throw new ArgumentNullException(nameof(target));

        if (!string.Equals(stop.OcoGroup, ocoGroup, StringComparison.Ordinal) ||
            !string.Equals(target.OcoGroup, ocoGroup, StringComparison.Ordinal))
            throw new InvalidOperationException("Both protective orders must use the same OCO group.");
    }

    public string OcoGroup { get; }
    public ProtectiveOrderInstruction Stop { get; }
    public ProtectiveOrderInstruction Target { get; }
}

public sealed class ProtectiveOrderTransition
{
    public ProtectiveOrderTransition(ProtectiveOrderKind kind, ProtectivePlatformOrderState state,
        string platformOrderId, string? siblingOrderId, bool emergencyFlattenRequired, string message)
    {
        Kind = kind;
        State = state;
        PlatformOrderId = platformOrderId ?? string.Empty;
        SiblingOrderId = siblingOrderId;
        EmergencyFlattenRequired = emergencyFlattenRequired;
        Message = message ?? string.Empty;
    }

    public ProtectiveOrderKind Kind { get; }
    public ProtectivePlatformOrderState State { get; }
    public string PlatformOrderId { get; }
    public string? SiblingOrderId { get; }
    public bool EmergencyFlattenRequired { get; }
    public string Message { get; }
}

public sealed class ProtectiveOrderCoordinator
{
    private readonly AuthoritativePositionManager _manager;
    private readonly Func<DateTime, string> _ocoFactory;
    private string? _stopOrderId;
    private string? _targetOrderId;

    public ProtectiveOrderCoordinator(AuthoritativePositionManager manager,
        Func<DateTime, string>? ocoFactory = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _ocoFactory = ocoFactory ?? (time => "ISE-OCO-" + time.ToString("yyyyMMddHHmmssfff"));
    }

    public PositionManagerSnapshot Current => _manager.Current;

    public ProtectiveOrderPair Plan(decimal stopPrice, decimal targetPrice, DateTime occurredAt)
    {
        var plan = _manager.CreateProtectivePlan(stopPrice, targetPrice, occurredAt);
        var oco = _ocoFactory(occurredAt);
        if (string.IsNullOrWhiteSpace(oco))
            throw new InvalidOperationException("The OCO factory returned an empty identifier.");

        _stopOrderId = null;
        _targetOrderId = null;

        var stop = new ProtectiveOrderInstruction(
            ProtectiveOrderKind.Stop,
            plan.AccountName,
            plan.Instrument,
            plan.ClosingSide,
            plan.Quantity,
            plan.StopPrice,
            oco,
            "ISE-PROTECT-STOP");

        var target = new ProtectiveOrderInstruction(
            ProtectiveOrderKind.Target,
            plan.AccountName,
            plan.Instrument,
            plan.ClosingSide,
            plan.Quantity,
            plan.TargetPrice,
            oco,
            "ISE-PROTECT-TARGET");

        return new ProtectiveOrderPair(oco, stop, target);
    }

    public PositionManagerSnapshot RecordSubmitted(ProtectiveOrderKind kind, string platformOrderId,
        DateTime occurredAt)
    {
        if (kind == ProtectiveOrderKind.Stop)
            _stopOrderId = platformOrderId;
        else
            _targetOrderId = platformOrderId;

        return _manager.RecordProtectiveOrder(kind, platformOrderId, occurredAt);
    }

    public ProtectiveOrderTransition HandleTransition(ProtectiveOrderKind kind,
        ProtectivePlatformOrderState state, string platformOrderId)
    {
        if (string.IsNullOrWhiteSpace(platformOrderId))
            throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId));

        var sibling = kind == ProtectiveOrderKind.Stop ? _targetOrderId : _stopOrderId;

        if (state == ProtectivePlatformOrderState.Filled)
        {
            return new ProtectiveOrderTransition(kind, state, platformOrderId, sibling, false,
                "Protective exit filled; cancel the sibling order if OCO cancellation has not completed.");
        }

        if (state == ProtectivePlatformOrderState.Rejected)
        {
            return new ProtectiveOrderTransition(kind, state, platformOrderId, sibling, true,
                "Protective order rejected; emergency flatten is required while a position remains open.");
        }

        if (state == ProtectivePlatformOrderState.Cancelled &&
            _manager.Current.ExpectedQuantity > 0)
        {
            return new ProtectiveOrderTransition(kind, state, platformOrderId, sibling, true,
                "Protective order cancelled while the position remains open; emergency flatten is required.");
        }

        return new ProtectiveOrderTransition(kind, state, platformOrderId, null, false,
            "Protective order transition recorded.");
    }

    public FlattenInstruction CreateEmergencyFlatten(string reason, DateTime occurredAt) =>
        _manager.CreateEmergencyFlatten(reason, occurredAt);

    public PositionManagerSnapshot Recover(BrokerPositionSnapshot broker,
        System.Collections.Generic.IReadOnlyCollection<BrokerWorkingOrder> workingOrders)
    {
        var snapshot = _manager.Recover(broker, workingOrders);
        _stopOrderId = snapshot.StopOrderId;
        _targetOrderId = snapshot.TargetOrderId;
        return snapshot;
    }
}
