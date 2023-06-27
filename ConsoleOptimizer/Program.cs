using Google.OrTools.LinearSolver;
using System.Diagnostics;
using System.Text;

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
                            double maxChargePower = 1.8,
                            double maxDischargePower = 0.9,
                            double roundtripEfficiencyFullpower = 0.81,
                            double roundtripEfficiencyHalfpower = 0.9
        )
    {
        var stopwatch = Stopwatch.StartNew();
        var allprices = SampleDays.RawHalf2023;
        var prices = new Tariff[24];
        int start = 0;
        int delta = 24; // new prices are published at 12:00
        double totalProfit = 0;
        while (true)
        {
            Array.Copy(allprices, start, prices, 0, prices.Length);
            var startMoment = prices[0].Timestamp;
            var dayprices = prices.Select(p => p.TariffUsage).ToArray();
            OptimizedState[] hours = Optimize(startEnergy,
                                              endEnergy,
                                              maxEnergy,
                                              maxChargePower,
                                              maxDischargePower,
                                              roundtripEfficiencyFullpower,
                                              roundtripEfficiencyHalfpower,
                                              dayprices,
                                              startMoment);
            var day = hours.Take(delta).ToArray();
            PrintGraphs(day, maxEnergy);
            var profitToday = day.Sum(h => -h.Cost);
            Console.WriteLine($" == Profit subtotal {profitToday:0.00}");
            totalProfit += profitToday;
            start += delta;
            delta = 24;
            var nextStretch = Math.Min(24, allprices.Length - start);
            if (nextStretch <= 0) break;
            prices = new Tariff[nextStretch];
            startEnergy = day.Last().EndSoC;
        }

        Console.WriteLine($" === Profit total {totalProfit:0.00}");
        Console.WriteLine($" Elapsed: {stopwatch.Elapsed}");
    }

    private static OptimizedState[] Optimize(double startEnergy, double endEnergy, double maxEnergy, double maxChargePower, double maxDischargePower, double roundtripEfficiencyFullpower, double roundtripEfficiencyHalfpower, double[] prices, DateTimeOffset startMoment)
    {
        HourPrice[] fullPowerPrices = HourPrice.FromAcPrices(prices,
         chargeEfficiency: 0.9, dischargeEfficiency: 0.95).ToArray();
        double[] SoCs = new double[prices.Length];
        double[] costs = new double[prices.Length];
        double[] charge = new double[prices.Length];
        double[] discharge = new double[prices.Length];
        var orderedPrices = prices.Select((price, index) => (price, index)).OrderBy(x => x.price).Select(x => x.index).ToArray();
        var cheapestHours = orderedPrices[0..2];
        var expensiveHours = orderedPrices[^4..^0];
        for (int hour = 0; hour < prices.Length; hour++)
        {
            if (cheapestHours.Contains(hour))
            {
                charge[hour] = Math.Min(maxChargePower, maxEnergy - startEnergy);
                costs[hour] = fullPowerPrices[hour].Charge * charge[hour];
            }
            else if (expensiveHours.Contains(hour))
            {
                discharge[hour] = Math.Min(maxDischargePower, startEnergy);
                costs[hour] = -fullPowerPrices[hour].Discharge * discharge[hour];
            }
            startEnergy += charge[hour] - discharge[hour];

            SoCs[hour] = startEnergy;
        }
        var hours = new PartialSolution
        {
            Prices = prices,
            SoCs = SoCs,
            Costs = costs,
            Charge = charge,
            Discharge = discharge
        }.ToHourStates(startMoment);
        return hours;
    }

    private static double[] Evaluate(LinearExpr[] exprs)
    {
        return exprs.Select(e => e.EvaluateSolution()).ToArray();
    }

    private static void PrintGraphs(IEnumerable<OptimizedState> hours, double maxEnergy)
    {
        var minPrice = hours.Min(p => p.GridPrice);
        var maxPrice = hours.Max(p => p.GridPrice);
        Console.WriteLine("Timestamp  | Price               | Charged energy       | Discharged energy    | SoC ");
        foreach (var state in hours)
        {
            Console.Write($"{state.Timestamp:MMddTHH:mm} |");
            WriteGraphLine(state.GridPrice, minPrice, maxPrice);
            WriteGraphLine(state.Charge, 0, maxEnergy);
            WriteGraphLine(state.Discharge, 0, maxEnergy);
            WriteGraphLine(state.EndSoC, 0, maxEnergy);
            Console.WriteLine();
        }
    }

    static void WriteGraphLine(double value, double min, double max)
    {
        int width = 15;
        int part = (int)Math.Round(width * ((value - min) / (max - min)));
        var line = new StringBuilder(width + 3);
        line.Append($"{value + 0.0001:0.00} ");
        line.Append('*', part);
        line.Append(' ', width - part);
        line.Append(" | ");
        Console.Write(line);
    }
}
