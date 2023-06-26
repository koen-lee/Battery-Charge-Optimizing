public record OptimizedState
{
    public DateTimeOffset Timestamp { get; init; }
    public TimeSpan SlotDuration => TimeSpan.FromHours(1); // perhaps future quarterly prices?
    public double GridPrice { get; init; }

    public double StartSoC => EndSoC + Discharge - Charge;
    /// <summary>
    /// SoC at the end of the timeslot
    /// </summary>
    public double EndSoC { get; init; }

    public double Cost { get; init; }
    public double Charge { get; init; }
    public double Discharge { get; init; }

}
