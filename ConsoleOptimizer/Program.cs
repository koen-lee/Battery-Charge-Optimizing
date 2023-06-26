﻿using Google.OrTools.LinearSolver;
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
                            double maxChargePower = 2.2,
                            double maxDischargePower = 1.7,
                            double roundtripEfficiencyFullpower = 0.81,
                            double roundtripEfficiencyHalfpower = 0.9
        )
    {
        var prices = SampleDays.Jun07_2023;
        HourPrice[] fullPowerPrices = HourPrice.FromAcPrices(prices,
         chargeEfficiency: Math.Sqrt(roundtripEfficiencyFullpower), dischargeEfficiency: Math.Sqrt(roundtripEfficiencyFullpower)).ToArray();
        HourPrice[] halfPowerPrices = HourPrice.FromAcPrices(prices,
             chargeEfficiency: Math.Sqrt(roundtripEfficiencyHalfpower), dischargeEfficiency: Math.Sqrt(roundtripEfficiencyHalfpower)).ToArray();

        Solver solver = Solver.CreateSolver("GLOP");

        var chargeFullpower = solver.MakeNumVarArray(prices.Length, 0, maxChargePower, "chargeFullpower");
        var dischargeFullpower = solver.MakeNumVarArray(prices.Length, 0, maxDischargePower, "dischargeFullpower");
        var chargeHalfpower = solver.MakeNumVarArray(prices.Length, 0, 0.5 * maxChargePower, "chargeHalfpower");
        var dischargeHalfpower = solver.MakeNumVarArray(prices.Length, 0, 0.5 * maxDischargePower, "dischargeHalfpower");

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
            charge[hour] = chargeFullpower[hour] + chargeHalfpower[hour];
            discharge[hour] = dischargeFullpower[hour] + dischargeHalfpower[hour];
            solver.Add(charge[hour] <= maxChargePower);
            solver.Add(discharge[hour] <= maxDischargePower);

            SoC = SoC + charge[hour] - discharge[hour];
            SoCs[hour] = SoC;
            // Add charge until full constraints
            solver.Add(SoC <= maxEnergy);
            solver.Add(SoC >= 0);
            // Calculate running profits
            costs[hour] = fullPowerPrices[hour].Charge * chargeFullpower[hour] + halfPowerPrices[hour].Charge * chargeHalfpower[hour]
                - fullPowerPrices[hour].Discharge * dischargeFullpower[hour] - halfPowerPrices[hour].Discharge * dischargeHalfpower[hour];
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
        }.ToHourStates(DateTimeOffset.MinValue);

        Console.WriteLine($" Result: €{Math.Round(solver.Objective().Value(), 4)}");

        PrintGraphs(hours, maxEnergy);
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
