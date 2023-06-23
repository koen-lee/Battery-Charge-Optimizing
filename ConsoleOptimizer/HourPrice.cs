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

    public static IEnumerable<HourPrice> FromAcPrices(IEnumerable<double> acPrices, double chargeEfficiency, double dischargeEfficiency)
    {
        foreach (var hourPrice in acPrices)
            yield return new HourPrice(hourPrice, chargeEfficiency, dischargeEfficiency);
    }
}
