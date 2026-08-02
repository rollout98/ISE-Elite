namespace ISE.Session;

/// <summary>Logical phase within the ISE Elite trading day.</summary>
public enum SessionPhase
{
    Maintenance,
    Evening,
    Overnight,
    Premarket,
    NewYorkOpen,
    RegularTrading,
    Closing
}
