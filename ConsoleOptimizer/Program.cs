using Google.OrTools.LinearSolver;

public static class Program
{
    public static void Main(double startEnergy = 4, double maxEnergy = 5, double maxChargePower = 2, double maxDischargePower = 3)
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

        solver.Maximize(profit);
        solver.SetTimeLimit(1000);
        var result = solver.Solve();
        Console.WriteLine(result.ToString());

        Console.WriteLine($" Result: €{Math.Round(solver.Objective().Value(), 2)}");

        var minPrice = prices.Min(p => p.Charge);
        var maxPrice = prices.Max(p => p.Charge);
        Console.WriteLine("Hour | Price          | Charge energy   | Disch. energy   | SoC ");
        for (int hour = 0; hour < prices.Length; hour++)
        {
            Console.Write($"{hour:00}   |");
            WriteGraphLine(prices[hour].Charge, minPrice, maxPrice);
            WriteGraphLine(chargeEnergies[hour].SolutionValue(), 0, maxEnergy);
            WriteGraphLine(dischargeEnergies[hour].SolutionValue(), 0, maxEnergy);
            startEnergy += chargeEnergies[hour].SolutionValue() - dischargeEnergies[hour].SolutionValue();
            WriteGraphLine(startEnergy, 0, maxEnergy);
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
        Console.Write(line);
        Console.Write(new string(' ', width - line.Length));
        Console.Write(" | ");
    }
}
