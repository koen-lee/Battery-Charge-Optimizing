public record HourPrice
{
    public double Charge { get; init; }

    public double Discharge { get; init; }

    public HourPrice(double charge, double discharge)
    {
        Charge = charge;
        Discharge = discharge;
    }

    public HourPrice(double acPrice, double chargeEfficiency, double dischargeEfficiency)
    {
        Charge = acPrice / chargeEfficiency;
        Discharge = acPrice * dischargeEfficiency;
    }

    public static IEnumerable<HourPrice> FromAcPrices(IEnumerable<double> acPrices, double chargeEfficiency = 0, double dischargeEfficiency = 0)
    {
        foreach (var hourPrice in acPrices)
            yield return new HourPrice((hourPrice + 0.15) * 1.21, chargeEfficiency, dischargeEfficiency);
    }
}
