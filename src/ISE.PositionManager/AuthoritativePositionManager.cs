using System;
using System.Collections.Generic;
using System.Linq;
using ISE.ExecutionCoordinator;

namespace ISE.PositionManager;

public enum FillAction { Buy, Sell, SellShort, BuyToCover }
public enum PositionSide { Flat, Long, Short }
public enum PositionManagerStatus
{
    Flat,
    OpenUnprotected,
    ProtectiveOrdersPending,
    Protected,
    Flattening,
    ReconciliationRequired
}
public enum ProtectiveOrderKind { Stop, Target }
public enum WorkingOrderKind { Stop, Target, Other }

public sealed class ExecutionFill
{
    public ExecutionFill(string executionId, string platformOrderId, string accountName, string instrument,
        FillAction action, int quantity, decimal price, DateTime occurredAt)
    {
        ExecutionId = Required(executionId, nameof(executionId));
        PlatformOrderId = Required(platformOrderId, nameof(platformOrderId));
        AccountName = Required(accountName, nameof(accountName));
        Instrument = Required(instrument, nameof(instrument));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (price <= 0m) throw new ArgumentOutOfRangeException(nameof(price));
        Action = action;
        Quantity = quantity;
        Price = price;
        OccurredAt = occurredAt;
    }

    public string ExecutionId { get; }
    public string PlatformOrderId { get; }
    public string AccountName { get; }
    public string Instrument { get; }
    public FillAction Action { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public DateTime OccurredAt { get; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
}

public sealed class BrokerPositionSnapshot
{
    public BrokerPositionSnapshot(string accountName, string instrument, PositionSide side, int quantity,
        decimal averagePrice, DateTime occurredAt)
    {
        AccountName = Required(accountName, nameof(accountName));
        Instrument = Required(instrument, nameof(instrument));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (side == PositionSide.Flat && quantity != 0)
            throw new ArgumentException("A flat position must have zero quantity.", nameof(quantity));
        if (side != PositionSide.Flat && quantity == 0)
            throw new ArgumentException("An open position must have positive quantity.", nameof(quantity));
        if (quantity > 0 && averagePrice <= 0m)
            throw new ArgumentOutOfRangeException(nameof(averagePrice));
        Side = side;
        Quantity = quantity;
        AveragePrice = averagePrice;
        OccurredAt = occurredAt;
    }

    public string AccountName { get; }
    public string Instrument { get; }
    public PositionSide Side { get; }
    public int Quantity { get; }
    public decimal AveragePrice { get; }
    public DateTime OccurredAt { get; }

    internal int SignedQuantity => Side switch
    {
        PositionSide.Long => Quantity,
        PositionSide.Short => -Quantity,
        _ => 0
    };

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
}

public sealed class BrokerWorkingOrder
{
    public BrokerWorkingOrder(string platformOrderId, WorkingOrderKind kind, ExecutionSide side,
        int quantity, decimal price)
    {
        PlatformOrderId = string.IsNullOrWhiteSpace(platformOrderId)
            ? throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId))
            : platformOrderId;
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (price <= 0m) throw new ArgumentOutOfRangeException(nameof(price));
        Kind = kind;
        Side = side;
        Quantity = quantity;
        Price = price;
    }

    public string PlatformOrderId { get; }
    public WorkingOrderKind Kind { get; }
    public ExecutionSide Side { get; }
    public int Quantity { get; }
    public decimal Price { get; }
}

public sealed class ProtectiveOrderPlan
{
    public ProtectiveOrderPlan(string accountName, string instrument, PositionSide positionSide,
        ExecutionSide closingSide, int quantity, decimal stopPrice, decimal targetPrice)
    {
        AccountName = accountName;
        Instrument = instrument;
        PositionSide = positionSide;
        ClosingSide = closingSide;
        Quantity = quantity;
        StopPrice = stopPrice;
        TargetPrice = targetPrice;
    }

    public string AccountName { get; }
    public string Instrument { get; }
    public PositionSide PositionSide { get; }
    public ExecutionSide ClosingSide { get; }
    public int Quantity { get; }
    public decimal StopPrice { get; }
    public decimal TargetPrice { get; }
}

public sealed class FlattenInstruction
{
    public FlattenInstruction(string accountName, string instrument, ExecutionSide side, int quantity, string reason)
    {
        AccountName = accountName;
        Instrument = instrument;
        Side = side;
        Quantity = quantity;
        Reason = reason;
    }

    public string AccountName { get; }
    public string Instrument { get; }
    public ExecutionSide Side { get; }
    public int Quantity { get; }
    public string Reason { get; }
}

public sealed class PositionAuditRecord
{
    public PositionAuditRecord(string eventName, string detail, DateTime occurredAt)
    {
        EventName = eventName;
        Detail = detail;
        OccurredAt = occurredAt;
    }

    public string EventName { get; }
    public string Detail { get; }
    public DateTime OccurredAt { get; }
}

public sealed class PositionManagerSnapshot
{
    public PositionManagerSnapshot(string accountName, string instrument, PositionSide expectedSide,
        int expectedQuantity, int brokerSignedQuantity, decimal averagePrice, PositionManagerStatus status,
        ProtectiveOrderPlan? protectivePlan, string? stopOrderId, string? targetOrderId)
    {
        AccountName = accountName;
        Instrument = instrument;
        ExpectedSide = expectedSide;
        ExpectedQuantity = expectedQuantity;
        BrokerSignedQuantity = brokerSignedQuantity;
        AveragePrice = averagePrice;
        Status = status;
        ProtectivePlan = protectivePlan;
        StopOrderId = stopOrderId;
        TargetOrderId = targetOrderId;
    }

    public string AccountName { get; }
    public string Instrument { get; }
    public PositionSide ExpectedSide { get; }
    public int ExpectedQuantity { get; }
    public int BrokerSignedQuantity { get; }
    public decimal AveragePrice { get; }
    public PositionManagerStatus Status { get; }
    public ProtectiveOrderPlan? ProtectivePlan { get; }
    public string? StopOrderId { get; }
    public string? TargetOrderId { get; }
}

public sealed class AuthoritativePositionManager
{
    private readonly string _accountName;
    private readonly string _instrument;
    private readonly HashSet<string> _executionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<PositionAuditRecord> _history = new List<PositionAuditRecord>();

    private int _expectedSignedQuantity;
    private int _brokerSignedQuantity;
    private decimal _averagePrice;
    private PositionManagerStatus _status = PositionManagerStatus.Flat;
    private ProtectiveOrderPlan? _protectivePlan;
    private string? _stopOrderId;
    private string? _targetOrderId;

    public AuthoritativePositionManager(string accountName, string instrument)
    {
        _accountName = string.IsNullOrWhiteSpace(accountName)
            ? throw new ArgumentException("Account name is required.", nameof(accountName))
            : accountName;
        _instrument = string.IsNullOrWhiteSpace(instrument)
            ? throw new ArgumentException("Instrument is required.", nameof(instrument))
            : instrument;
    }

    public IReadOnlyList<PositionAuditRecord> History => _history.AsReadOnly();
    public PositionManagerSnapshot Current => Snapshot();

    public PositionManagerSnapshot ApplyFill(ExecutionFill fill)
    {
        if (fill == null) throw new ArgumentNullException(nameof(fill));
        ValidateScope(fill.AccountName, fill.Instrument);

        if (!_executionIds.Add(fill.ExecutionId))
        {
            Record("DuplicateFillIgnored", fill.ExecutionId, fill.OccurredAt);
            return Snapshot();
        }

        var delta = SignedDelta(fill.Action, fill.Quantity);
        var oldQuantity = _expectedSignedQuantity;
        var newQuantity = oldQuantity + delta;

        if (oldQuantity == 0 || SameSign(oldQuantity, delta))
        {
            _averagePrice = newQuantity == 0
                ? 0m
                : ((_averagePrice * Math.Abs(oldQuantity)) + (fill.Price * Math.Abs(delta))) /
                  Math.Abs(newQuantity);
        }
        else if (Math.Abs(delta) == Math.Abs(oldQuantity))
        {
            _averagePrice = 0m;
        }
        else if (Math.Abs(delta) > Math.Abs(oldQuantity))
        {
            _averagePrice = fill.Price;
        }

        _expectedSignedQuantity = newQuantity;
        ClearProtection();
        _status = newQuantity == 0 ? PositionManagerStatus.Flat : PositionManagerStatus.OpenUnprotected;
        Record("FillApplied", fill.ExecutionId + " signedDelta=" + delta, fill.OccurredAt);
        return Snapshot();
    }

    public PositionManagerSnapshot Reconcile(BrokerPositionSnapshot broker)
    {
        if (broker == null) throw new ArgumentNullException(nameof(broker));
        ValidateScope(broker.AccountName, broker.Instrument);
        _brokerSignedQuantity = broker.SignedQuantity;

        if (_brokerSignedQuantity != _expectedSignedQuantity)
        {
            _status = PositionManagerStatus.ReconciliationRequired;
            Record("ReconciliationMismatch",
                "expected=" + _expectedSignedQuantity + " broker=" + _brokerSignedQuantity,
                broker.OccurredAt);
            return Snapshot();
        }

        if (_brokerSignedQuantity != 0)
            _averagePrice = broker.AveragePrice;
        else
            _averagePrice = 0m;

        _status = DetermineOpenStatus();
        Record("Reconciled", "signedQuantity=" + _brokerSignedQuantity, broker.OccurredAt);
        return Snapshot();
    }

    public ProtectiveOrderPlan CreateProtectivePlan(decimal stopPrice, decimal targetPrice, DateTime occurredAt)
    {
        if (_expectedSignedQuantity == 0)
            throw new InvalidOperationException("A protective plan requires an open position.");
        if (_status == PositionManagerStatus.ReconciliationRequired)
            throw new InvalidOperationException("Protection cannot be planned while reconciliation is required.");
        if (stopPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(stopPrice));
        if (targetPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(targetPrice));

        var side = SideOf(_expectedSignedQuantity);
        if (side == PositionSide.Long && !(stopPrice < _averagePrice && targetPrice > _averagePrice))
            throw new InvalidOperationException("Long protection requires stop below entry and target above entry.");
        if (side == PositionSide.Short && !(stopPrice > _averagePrice && targetPrice < _averagePrice))
            throw new InvalidOperationException("Short protection requires stop above entry and target below entry.");

        var closingSide = side == PositionSide.Long ? ExecutionSide.Sell : ExecutionSide.Buy;
        _protectivePlan = new ProtectiveOrderPlan(_accountName, _instrument, side, closingSide,
            Math.Abs(_expectedSignedQuantity), stopPrice, targetPrice);
        _stopOrderId = null;
        _targetOrderId = null;
        _status = PositionManagerStatus.ProtectiveOrdersPending;
        Record("ProtectivePlanCreated", "stop=" + stopPrice + " target=" + targetPrice, occurredAt);
        return _protectivePlan;
    }

    public PositionManagerSnapshot RecordProtectiveOrder(ProtectiveOrderKind kind, string platformOrderId,
        DateTime occurredAt)
    {
        if (_protectivePlan == null)
            throw new InvalidOperationException("No protective plan is active.");
        if (string.IsNullOrWhiteSpace(platformOrderId))
            throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId));

        if (kind == ProtectiveOrderKind.Stop)
            _stopOrderId = RecordSingleOrder(_stopOrderId, platformOrderId, "stop");
        else
            _targetOrderId = RecordSingleOrder(_targetOrderId, platformOrderId, "target");

        _status = _stopOrderId != null && _targetOrderId != null
            ? PositionManagerStatus.Protected
            : PositionManagerStatus.ProtectiveOrdersPending;
        Record("ProtectiveOrderRecorded", kind + "=" + platformOrderId, occurredAt);
        return Snapshot();
    }

    public FlattenInstruction CreateEmergencyFlatten(string reason, DateTime occurredAt)
    {
        var signedQuantity = _brokerSignedQuantity != 0 ? _brokerSignedQuantity : _expectedSignedQuantity;
        if (signedQuantity == 0)
            throw new InvalidOperationException("No open position is available to flatten.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A flatten reason is required.", nameof(reason));

        _status = PositionManagerStatus.Flattening;
        var instruction = new FlattenInstruction(_accountName, _instrument,
            signedQuantity > 0 ? ExecutionSide.Sell : ExecutionSide.Buy,
            Math.Abs(signedQuantity), reason);
        Record("EmergencyFlattenCreated", reason, occurredAt);
        return instruction;
    }

    public PositionManagerSnapshot Recover(BrokerPositionSnapshot broker,
        IReadOnlyCollection<BrokerWorkingOrder> workingOrders)
    {
        if (broker == null) throw new ArgumentNullException(nameof(broker));
        if (workingOrders == null) throw new ArgumentNullException(nameof(workingOrders));
        ValidateScope(broker.AccountName, broker.Instrument);

        _executionIds.Clear();
        _expectedSignedQuantity = broker.SignedQuantity;
        _brokerSignedQuantity = broker.SignedQuantity;
        _averagePrice = broker.AveragePrice;
        ClearProtection();

        if (_expectedSignedQuantity == 0)
        {
            _status = PositionManagerStatus.Flat;
            Record("RecoveredFlat", "Broker reported flat.", broker.OccurredAt);
            return Snapshot();
        }

        var positionSide = SideOf(_expectedSignedQuantity);
        var closingSide = positionSide == PositionSide.Long ? ExecutionSide.Sell : ExecutionSide.Buy;
        var quantity = Math.Abs(_expectedSignedQuantity);
        var stop = workingOrders.FirstOrDefault(order => order.Kind == WorkingOrderKind.Stop &&
            order.Side == closingSide && order.Quantity == quantity);
        var target = workingOrders.FirstOrDefault(order => order.Kind == WorkingOrderKind.Target &&
            order.Side == closingSide && order.Quantity == quantity);

        if (stop != null && target != null && ProtectionGeometryIsValid(positionSide, stop.Price, target.Price))
        {
            _protectivePlan = new ProtectiveOrderPlan(_accountName, _instrument, positionSide, closingSide,
                quantity, stop.Price, target.Price);
            _stopOrderId = stop.PlatformOrderId;
            _targetOrderId = target.PlatformOrderId;
            _status = PositionManagerStatus.Protected;
            Record("RecoveredProtected", "stop=" + _stopOrderId + " target=" + _targetOrderId,
                broker.OccurredAt);
        }
        else
        {
            _status = PositionManagerStatus.OpenUnprotected;
            Record("RecoveredUnprotected", "Protective orders were not fully reconciled.", broker.OccurredAt);
        }

        return Snapshot();
    }

    private PositionManagerSnapshot Snapshot() => new PositionManagerSnapshot(
        _accountName,
        _instrument,
        SideOf(_expectedSignedQuantity),
        Math.Abs(_expectedSignedQuantity),
        _brokerSignedQuantity,
        _averagePrice,
        _status,
        _protectivePlan,
        _stopOrderId,
        _targetOrderId);

    private PositionManagerStatus DetermineOpenStatus()
    {
        if (_expectedSignedQuantity == 0)
            return PositionManagerStatus.Flat;
        if (_protectivePlan == null)
            return PositionManagerStatus.OpenUnprotected;
        return _stopOrderId != null && _targetOrderId != null
            ? PositionManagerStatus.Protected
            : PositionManagerStatus.ProtectiveOrdersPending;
    }

    private bool ProtectionGeometryIsValid(PositionSide side, decimal stopPrice, decimal targetPrice) =>
        side == PositionSide.Long
            ? stopPrice < _averagePrice && targetPrice > _averagePrice
            : stopPrice > _averagePrice && targetPrice < _averagePrice;

    private void ClearProtection()
    {
        _protectivePlan = null;
        _stopOrderId = null;
        _targetOrderId = null;
    }

    private void ValidateScope(string accountName, string instrument)
    {
        if (!string.Equals(accountName, _accountName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Position update belongs to a different account.");
        if (!string.Equals(instrument, _instrument, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Position update belongs to a different instrument.");
    }

    private void Record(string eventName, string detail, DateTime occurredAt) =>
        _history.Add(new PositionAuditRecord(eventName, detail, occurredAt));

    private static string RecordSingleOrder(string? existing, string incoming, string kind)
    {
        if (existing != null && !string.Equals(existing, incoming, StringComparison.Ordinal))
            throw new InvalidOperationException("A different " + kind + " order is already recorded.");
        return incoming;
    }

    private static int SignedDelta(FillAction action, int quantity) => action switch
    {
        FillAction.Buy => quantity,
        FillAction.BuyToCover => quantity,
        FillAction.Sell => -quantity,
        FillAction.SellShort => -quantity,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static bool SameSign(int left, int right) => (left > 0 && right > 0) || (left < 0 && right < 0);

    private static PositionSide SideOf(int signedQuantity) => signedQuantity switch
    {
        > 0 => PositionSide.Long,
        < 0 => PositionSide.Short,
        _ => PositionSide.Flat
    };
}
