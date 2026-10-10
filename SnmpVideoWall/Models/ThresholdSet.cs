using System.Text.Json.Serialization;

namespace SnmpVideoWall.Models;

public sealed class ThresholdSet
{
    [JsonPropertyName("rxLowBitsPerSecond")] public double RxLowBps { get; set; }
    [JsonPropertyName("rxHighBitsPerSecond")] public double RxHighBps { get; set; }
    [JsonPropertyName("txLowBitsPerSecond")] public double TxLowBps { get; set; }
    [JsonPropertyName("txHighBitsPerSecond")] public double TxHighBps { get; set; }
}
