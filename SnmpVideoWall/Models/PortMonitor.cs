using System.Text.Json.Serialization;

namespace SnmpVideoWall.Models;

public sealed class PortMonitor
{
    public string Name { get; set; } = "Device";
    public string Ip { get; set; } = "127.0.0.1";
    public string Community { get; set; } = "public";
    public string Interface { get; set; } = "Port";
    public string RxOid { get; set; } = "1.3.6.1.2.1.2.2.1.10.1";
    public string TxOid { get; set; } = "1.3.6.1.2.1.2.2.1.16.1";
    [System.Text.Json.Serialization.JsonPropertyName("capacityBitsPerSecond")] public long CapacityBps { get; set; } = 1_000_000_000;
    public bool UseCustomThresholds { get; set; }
    public ThresholdSet DayThresholds { get; set; } = new();
    public ThresholdSet NightThresholds { get; set; } = new();
    // Legacy fields are retained so older monitors.json files continue to load.
    [System.Text.Json.Serialization.JsonPropertyName("rxLowBitsPerSecond")] public double RxLowThresholdBps { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("rxHighBitsPerSecond")] public double RxHighThresholdBps { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("txLowBitsPerSecond")] public double TxLowThresholdBps { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("txHighBitsPerSecond")] public double TxHighThresholdBps { get; set; }

    [JsonIgnore] public double RxBps { get; set; }
    [JsonIgnore] public double TxBps { get; set; }
    [JsonIgnore] public bool Online { get; set; }
    [JsonIgnore] public bool HasRateSample { get; set; }
    [JsonIgnore] public string Error { get; set; } = "";
    [JsonIgnore] public List<double> RxHistory { get; } = new();
    [JsonIgnore] public List<double> TxHistory { get; } = new();
    [JsonIgnore] public List<DateTime> HistoryTimes { get; } = new();
    [JsonIgnore] public ulong? PreviousRx { get; set; }
    [JsonIgnore] public ulong? PreviousTx { get; set; }
    [JsonIgnore] public DateTime? PreviousSampleUtc { get; set; }
    [JsonIgnore] public object? Tag { get; set; }
    [JsonIgnore] public ThresholdSet ActiveThresholds { get; set; } = new();

    [JsonIgnore]
    public List<string> ActiveAlerts
    {
        get
        {
            var t = ActiveThresholds;
            var alerts = new List<string>();
            if (!Online || !HasRateSample) return alerts;
            if (t.RxLowBps > 0 && RxBps < t.RxLowBps) alerts.Add($"RX LOW: {Format(RxBps)} < {Format(t.RxLowBps)}");
            if (t.RxHighBps > 0 && RxBps > t.RxHighBps) alerts.Add($"RX HIGH: {Format(RxBps)} > {Format(t.RxHighBps)}");
            if (t.TxLowBps > 0 && TxBps < t.TxLowBps) alerts.Add($"TX LOW: {Format(TxBps)} < {Format(t.TxLowBps)}");
            if (t.TxHighBps > 0 && TxBps > t.TxHighBps) alerts.Add($"TX HIGH: {Format(TxBps)} > {Format(t.TxHighBps)}");
            return alerts;
        }
    }
    private static string Format(double bps)
    {
        string[] units = { "bps", "Kbps", "Mbps", "Gbps", "Tbps" }; int i = 0;
        while (Math.Abs(bps) >= 1000 && i < units.Length - 1) { bps /= 1000; i++; }
        return $"{bps:0.##} {units[i]}";
    }
}
