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
    /// <param name="chargemap">List of power:efficiency pairs for charging, ;-separated, in kW/fraction</param>
    /// <param name="dischargemap">List of power:efficiency pairs for discharging, ;-separated, in kW/fraction.</param>
    public static void Main(double startEnergy = 0,
                            double endEnergy = 0,
                            double maxEnergy = 5,
                            string chargemap = "1.1:0.95;1.65:0.93;2.2:0.90",
                            string dischargemap = "0.85:0.95;1.2:0.93;1.7:0.92"
        )
    {
        var thechargemap = chargemap.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(EfficiencyMap.FromSimpleString).ToArray();
        var thedischargemap = dischargemap.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(EfficiencyMap.FromSimpleString).ToArray();

        var stopwatch = Stopwatch.StartNew();
        var allprices = SampleDays.RawHalf2023;
        var prices = new Tariff[24];
        int start = 0;
        int delta = 12; // new prices are published at 12:00
        double totalProfit = 0;
        while (true)
        {
            Array.Copy(allprices, start, prices, 0, prices.Length);
            var startMoment = prices[0].Timestamp;
            var dayprices = prices.Select(p => p.TariffUsage).ToArray();
            OptimizedState[] hours = Optimize(startEnergy,
                                              endEnergy,
                                              maxEnergy,
                                              thechargemap,
                                              thedischargemap,
                                              dayprices,
                                              startMoment);
            var day = hours.Take(delta).ToArray();
            PrintGraphs(day, maxEnergy);
            var profitToday = day.Sum(h => -h.Cost);
            Console.WriteLine($" == Profit subtotal {profitToday:0.00}");
            totalProfit += profitToday;
            start += delta;
            delta = 24;
            var nextStretch = Math.Min(36, allprices.Length - start);
            if (nextStretch <= 0) break;
            prices = new Tariff[nextStretch];
            startEnergy = day.Last().EndSoC;
        }

        Console.WriteLine($" === Profit total {totalProfit:0.00}");
        Console.WriteLine($" Elapsed: {stopwatch.Elapsed}");
    }

    private static OptimizedState[] Optimize(double startEnergy,
                                             double endEnergy,
                                             double maxEnergy,
                                             EfficiencyMap[] chargemap,
                                             EfficiencyMap[] dischargemap,
                                             double[] prices,
                                             DateTimeOffset startMoment)
    {
        double maxChargePower = chargemap.Select(c => c.Power).Max();
        double maxDischargePower = dischargemap.Select(c => c.Power).Max();
        Dictionary<EfficiencyMap, HourPrice[]> chargePrices = chargemap.ToDictionary(c => c, c => HourPrice.FromAcPrices(prices,
            chargeEfficiency: c.Efficiency).ToArray());
        Dictionary<EfficiencyMap, HourPrice[]> dischargePrices = dischargemap.ToDictionary(c => c, c => HourPrice.FromAcPrices(prices,
            dischargeEfficiency: c.Efficiency).ToArray());

        var solver = Solver.CreateSolver("GLOP");

        var charges = chargemap.ToDictionary(c => c, c => solver.MakeNumVarArray(prices.Length, 0, c.Power, "charge" + c.Power));
        var discharges = dischargemap.ToDictionary(c => c, c => solver.MakeNumVarArray(prices.Length, 0, c.Power, "discharge" + c.Power));

        // Growing expressions
        LinearExpr SoC = new LinearExpr() + startEnergy;
        LinearExpr profit = new();
        // hourly state
        LinearExpr[] charge = new LinearExpr[prices.Length];
        LinearExpr[] discharge = new LinearExpr[prices.Length];
        LinearExpr[] costs = new LinearExpr[prices.Length];
        LinearExpr[] SoCs = new LinearExpr[prices.Length];
        for (int hour = 0; hour < prices.Length; hour++)
        {
            // Make sure total power is limited
            charge[hour] = charges.Values.Select(c => c[hour]).ToArray().Sum();
            discharge[hour] = discharges.Values.Select(c => c[hour]).ToArray().Sum();
            solver.Add(charge[hour] <= maxChargePower);
            solver.Add(discharge[hour] <= maxDischargePower);

            SoC = SoC + charge[hour] - discharge[hour];
            SoCs[hour] = SoC;
            // Add charge until full constraints
            solver.Add(SoC <= maxEnergy);
            solver.Add(SoC >= 0);
            // Calculate running profits
            costs[hour] = chargemap.Select(c => charges[c][hour] * chargePrices[c][hour].Charge).ToArray().Sum()
                - dischargemap.Select(c => discharges[c][hour] * dischargePrices[c][hour].Discharge).ToArray().Sum();
            profit -= costs[hour];
        }
        solver.Add(SoC >= endEnergy);

        solver.Maximize(profit);
        solver.SetTimeLimit(1000); // just in case
        var result = solver.Solve();
        Console.WriteLine(result.ToString());
        var hours = new PartialSolution
        {
            Prices = prices,
            SoCs = Evaluate(SoCs),
            Costs = Evaluate(costs),
            Charge = Evaluate(charge),
            Discharge = Evaluate(discharge)
        }.ToHourStates(startMoment);
        return hours;
    }

    private static double[] Evaluate(LinearExpr[] exprs)
    {
        return exprs.Select(e => e.SolutionValue()).ToArray();
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

    public struct EfficiencyMap
    {
        public static EfficiencyMap FromSimpleString(string simpleserialized)
        {
            var members = simpleserialized.Split(':');
            return new EfficiencyMap
            {
                Power = double.Parse(members[0], System.Globalization.CultureInfo.InvariantCulture),
                Efficiency = double.Parse(members[1], System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        private double efficiency;

        public double Efficiency
        {
            get => efficiency;
            init
            {
                if (efficiency > 1 || efficiency < 0) throw new ArgumentOutOfRangeException();
                efficiency = value;
            }
        }

        public double Power { get; init; }
    }
}
