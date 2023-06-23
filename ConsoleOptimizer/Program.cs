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
    public static void Main(double startEnergy = 2, double endEnergy = 1, double maxEnergy = 5, double maxChargePower = 2, double maxDischargePower = 2.2)
    {
        HourPrice[] prices = HourPrice.FromAcPrices(SampleDays.Oct31_2022,
         chargeEfficiency: 0.9, dischargeEfficiency: 0.9).ToArray();

        Solver solver = Solver.CreateSolver("GLOP");

        var chargeEnergies = solver.MakeNumVarArray(prices.Length, 0, maxChargePower);
        var dischargeEnergies = solver.MakeNumVarArray(prices.Length, 0, maxDischargePower);

        LinearExpr SoC = new LinearExpr() + startEnergy;
        LinearExpr profit = new();
        for (int hour = 0; hour < prices.Length; hour++)
        {
            // Add charge until full constraints
            solver.Add(SoC + chargeEnergies[hour] <= maxEnergy);
            solver.Add(SoC - dischargeEnergies[hour] >= 0);
            SoC = SoC + chargeEnergies[hour] - dischargeEnergies[hour];

            // Calculate running profits
            profit = profit - prices[hour].Charge * chargeEnergies[hour] + prices[hour].Discharge * dischargeEnergies[hour];
        }
        solver.Add(SoC >= endEnergy);

        solver.Maximize(profit);
        solver.SetTimeLimit(1000); // just in case
        var result = solver.Solve();
        Console.WriteLine(result.ToString());

        Console.WriteLine($" Result: €{Math.Round(solver.Objective().Value(), 2)}");
        PrintGraphs(startEnergy, maxEnergy, prices, chargeEnergies, dischargeEnergies);
    }

    private static void PrintGraphs(double lastSoC, double maxEnergy, HourPrice[] prices, Variable[] chargeEnergies, Variable[] dischargeEnergies)
    {
        var minPrice = prices.Min(p => p.Charge);
        var maxPrice = prices.Max(p => p.Charge);
        Console.WriteLine("Hour | Price               | Charged energy       | Discharged energy    | SoC ");
        for (int hour = 0; hour < prices.Length; hour++)
        {
            Console.Write($"{hour:00}   |");
            WriteGraphLine(prices[hour].Charge, minPrice, maxPrice);
            WriteGraphLine(chargeEnergies[hour].SolutionValue(), 0, maxEnergy);
            WriteGraphLine(dischargeEnergies[hour].SolutionValue(), 0, maxEnergy);
            lastSoC += chargeEnergies[hour].SolutionValue() - dischargeEnergies[hour].SolutionValue();
            WriteGraphLine(lastSoC, 0, maxEnergy);
            Console.WriteLine();
        }
    }

    static void WriteGraphLine(double value, double min, double max)
    {
        int width = 15;
        double part = width * ((value - min) / (max - min));
        string line = new string('*', (int)Math.Floor(part));
        if (part - line.Length > 0.5)
            line += '-';
        Console.Write($"{value+0.0001:0.00} ");
        Console.Write(line);
        Console.Write(new string(' ', width - line.Length));
        Console.Write(" | ");
    }
}
