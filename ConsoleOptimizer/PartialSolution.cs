public record PartialSolution
{
    public double[] Prices { get; internal set; }
    public double[] SoCs { get; internal set; }
    public double[] Costs { get; internal set; }
    public double[] Charge { get; internal set; }
    public double[] Discharge { get; internal set; }

    public OptimizedState[] ToHourStates(DateTimeOffset from)
    {
        if (Prices.Length != SoCs.Length || Prices.Length != Costs.Length || Prices.Length != Charge.Length || Prices.Length != Discharge.Length)
            throw new InvalidOperationException("Length mismatch");
        var result = new OptimizedState[Prices.Length];
        for (int i = 0; i < Prices.Length; i++)
        {
            result[i] = new OptimizedState
            {
                Timestamp = from,
                GridPrice = Prices[i],
                Charge = Charge[i],
                Cost = Costs[i],
                Discharge = Discharge[i],
                EndSoC = SoCs[i],
            };

            from += result[i].SlotDuration;
        }
        return result;
    }
}
