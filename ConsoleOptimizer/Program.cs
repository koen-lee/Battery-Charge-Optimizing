using Google.OrTools.LinearSolver;

public static class Program
{
    /// <summary>
    /// Generates a charge/discharge schedule
    /// </summary>
    /// <param name="startEnergy">Start battery level, in kWh</param>
    /// <param name="endEnergy">Desired end battery level, in kWh</param>
    /// <param name="maxEnergy">Maximum battery capacity, in kWh</param>
    /// <param name="maxChargePower">Maximum charger power (DC side)</param>
    /// <param name="maxDischargePower">Maximum discharge power (DC side)</param>
    /// <param name="roundtripEfficiencyFullpower">Full power efficiency, the roundtrip efficiency is evenly distributed between charge- and discharge efficiencies</param>
    /// <param name="roundtripEfficiencyHalfpower">Part load efficiency, usually higher than full load efficiency.</param>
    public static void Main(double startEnergy = 0,
                            double endEnergy = 0,
                            double maxEnergy = 5,
                            double maxChargePower = 2.2,
                            double maxDischargePower = 1.7,
                            double roundtripEfficiencyFullpower = 0.9,
                            double roundtripEfficiencyHalfpower = 0.9
        )
    {
        HourPrice[] fullPowerPrices = HourPrice.FromAcPrices(SampleDays.Jun07_2023,
         chargeEfficiency: Math.Sqrt(roundtripEfficiencyFullpower), dischargeEfficiency: Math.Sqrt(roundtripEfficiencyFullpower)).ToArray();
        HourPrice[] halfPowerPrices = HourPrice.FromAcPrices(SampleDays.Jun07_2023,
             chargeEfficiency: Math.Sqrt(roundtripEfficiencyHalfpower), dischargeEfficiency: Math.Sqrt(roundtripEfficiencyHalfpower)).ToArray();

        Solver solver = Solver.CreateSolver("GLOP");

        var chargeFullpower = solver.MakeNumVarArray(fullPowerPrices.Length, 0, maxChargePower);
        var dischargeFullpower = solver.MakeNumVarArray(fullPowerPrices.Length, 0, maxDischargePower);
        var chargeHalfpower = solver.MakeNumVarArray(fullPowerPrices.Length, 0, 0.5 * maxChargePower);
        var dischargeHalfpower = solver.MakeNumVarArray(fullPowerPrices.Length, 0, 0.5 * maxDischargePower);

        LinearExpr SoC = new LinearExpr() + startEnergy;
        LinearExpr profit = new();
        for (int hour = 0; hour < fullPowerPrices.Length; hour++)
        {
            // Make sure total power is limited
            solver.Add(chargeFullpower[hour] + chargeHalfpower[hour] <= maxChargePower);
            solver.Add(dischargeFullpower[hour] + dischargeHalfpower[hour] <= maxDischargePower);

            // Add charge until full constraints
            solver.Add(SoC + chargeFullpower[hour] + chargeHalfpower[hour] <= maxEnergy);
            solver.Add(SoC - dischargeFullpower[hour] - dischargeHalfpower[hour] >= 0);
            SoC = SoC + chargeFullpower[hour] - dischargeFullpower[hour];
            SoC = SoC + chargeHalfpower[hour] - dischargeHalfpower[hour];

            // Calculate running profits
            profit = profit - fullPowerPrices[hour].Charge * chargeFullpower[hour] + fullPowerPrices[hour].Discharge * dischargeFullpower[hour];
            profit = profit - halfPowerPrices[hour].Charge * chargeHalfpower[hour] + halfPowerPrices[hour].Discharge * dischargeHalfpower[hour];
        }
        solver.Add(SoC >= endEnergy);

        solver.Maximize(profit);
        solver.SetTimeLimit(1000); // just in case
        var result = solver.Solve();
        Console.WriteLine(result.ToString());

        Console.WriteLine($" Result: €{Math.Round(solver.Objective().Value(), 4)}");
        PrintGraphs(startEnergy, maxEnergy, fullPowerPrices, chargeFullpower, dischargeFullpower, chargeHalfpower, dischargeHalfpower);
    }

    private static void PrintGraphs(double lastSoC, double maxEnergy, HourPrice[] prices, Variable[] chargeEnergies, Variable[] dischargeEnergies, Variable[] chargeHalfpower, Variable[] dischargeHalfpower)
    {
        var minPrice = prices.Min(p => p.Charge);
        var maxPrice = prices.Max(p => p.Charge);
        Console.WriteLine("Hour | Price               | Charged energy       | Discharged energy    | SoC ");
        for (int hour = 0; hour < prices.Length; hour++)
        {
            Console.Write($"{hour:00}   |");
            WriteGraphLine(prices[hour].Charge, minPrice, maxPrice);
            WriteGraphLine(chargeEnergies[hour].SolutionValue() + chargeHalfpower[hour].SolutionValue(), 0, maxEnergy);
            WriteGraphLine(dischargeEnergies[hour].SolutionValue() + dischargeHalfpower[hour].SolutionValue(), 0, maxEnergy);
            lastSoC += chargeEnergies[hour].SolutionValue() - dischargeEnergies[hour].SolutionValue();
            lastSoC += chargeHalfpower[hour].SolutionValue() - dischargeHalfpower[hour].SolutionValue();
            WriteGraphLine(lastSoC, 0, maxEnergy);
            Console.WriteLine();
        }
    }

    static void WriteGraphLine(double value, double min, double max)
    {
        int width = 15;
        double part = width * ((value - min) / (max - min));
        string line = new string('*', (int)Math.Round(part));
        if (part - line.Length > 0.5)
            line += '-';
        Console.Write($"{value + 0.0001:0.00} ");
        Console.Write(line);
        Console.Write(new string(' ', width - line.Length));
        Console.Write(" | ");
    }
}
