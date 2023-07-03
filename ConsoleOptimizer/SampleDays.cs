using System.Text.Json;

public static class SampleDays
{
    public static double[] Oct31_2022 = new double[] {
        0.13,
        0.13,
        0.12,
        0.13,
        0.13,
        0.16,
        0.19,
        0.19,
        0.16,
        0.15,
        0.15,
        0.15,
        0.16,
        0.17,
        0.19,
        0.19,
        0.21,
        0.2,
        0.19,
        0.16,
        0.15,
        0.13,
        0.11,
        0.1,
    };
    public static double[] Jun07_2023 = new double[] {
        0.08966,
        0.07836,
        0.07240,
        0.08876,
        0.07986,
        0.08019,
        0.07600,
        0.09720,
        0.11125,
        0.09422,
        0.09160,
        0.08569,
        0.07995,
        0.07030,
        0.07320,
        0.07226,
        0.07880,
        0.07570,
        0.09700,
        0.09993,
        0.11780,
        0.11230,
        0.10489,
        0.09490
    };

    // from unknown source
    public static Tariff[] Raw2022 => JsonSerializer.Deserialize<PricePoints>(File.OpenRead("energyprices2022.json")).Prices
        .Select(p => new Tariff { TariffUsage = p.price, Timestamp = p.readingDate }).ToArray();

    // result of curl "https://mijn.easyenergy.com/nl/api/tariff/getapxtariffs?startTimestamp=2023-01-01T00%3A00%3A00.000Z&endTimestamp=2023-06-27T00%3A00%3A00.000Z" -o 2023_firsthalf.json
    public static Tariff[] RawHalf2023 = JsonSerializer.Deserialize<Tariff[]>(File.OpenRead("energyprices2023_firsthalf.json"));
    public static readonly Stream Sample = File.OpenRead("energyprices2023_firsthalf.json");
}

public struct Tariff
{
    public DateTimeOffset Timestamp { get; init; }
    public double TariffUsage { get; init; }
}

public class PricePoints
{
    public PricePoint[] Prices { get; set; }
}
public struct PricePoint
{
    public DateTimeOffset readingDate { get; init; }
    public double price { get; init; }
}