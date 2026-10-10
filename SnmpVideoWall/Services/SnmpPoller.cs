using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpVideoWall.Models;
using System.Net;

namespace SnmpVideoWall.Services;

public sealed class SnmpPoller
{
    public int TimeoutMilliseconds { get; set; } = 1500;
    public async Task PollAsync(PortMonitor m, CancellationToken ct)
    {
        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(m.Ip), 161);
            var variables = new List<Variable> { new(new ObjectIdentifier(m.RxOid)), new(new ObjectIdentifier(m.TxOid)) };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Math.Max(250, TimeoutMilliseconds));
            var result = await Messenger.GetAsync(VersionCode.V2, endpoint,
                new OctetString(m.Community), variables, timeoutCts.Token).ConfigureAwait(false);
            var sampleTime = DateTime.UtcNow;
            var elapsed = m.PreviousSampleUtc.HasValue ? Math.Max(0.001, (sampleTime - m.PreviousSampleUtc.Value).TotalSeconds) : 1.0;
            if (result.Count < 2) throw new InvalidOperationException("SNMP returned fewer than 2 OIDs.");
            var rx = ToUInt64(result[0].Data);
            var tx = ToUInt64(result[1].Data);
            if (m.PreviousRx.HasValue && m.PreviousTx.HasValue)
            {
                m.RxBps = DeltaBps(m.PreviousRx.Value, rx, elapsed);
                m.TxBps = DeltaBps(m.PreviousTx.Value, tx, elapsed);
                m.HasRateSample = true;
            }
            m.PreviousRx = rx; m.PreviousTx = tx; m.PreviousSampleUtc = sampleTime;
            m.Online = true; m.Error = "";
            Add(m.RxHistory, m.RxBps); Add(m.TxHistory, m.TxBps);
            m.HistoryTimes.Add(DateTime.UtcNow);
            while (m.HistoryTimes.Count > 14400) m.HistoryTimes.RemoveAt(0);
        }
        catch (Exception ex)
        {
            m.Online = false; m.Error = ex.Message;
        }
    }

    private static ulong ToUInt64(ISnmpData d) => d switch
    {
        Counter64 c => c.ToUInt64(),
        Counter32 c => c.ToUInt32(),
        Gauge32 g => g.ToUInt32(),
        _ => throw new InvalidOperationException($"OID returned {d.TypeCode}, expected Counter32/Counter64.")
    };

    private static double DeltaBps(ulong oldValue, ulong newValue, double seconds)
    {
        ulong delta;
        if (newValue >= oldValue) delta = newValue - oldValue;
        else if (oldValue <= uint.MaxValue && newValue <= uint.MaxValue) delta = ((ulong)uint.MaxValue - oldValue) + newValue + 1;
        else delta = (ulong.MaxValue - oldValue) + newValue + 1;
        return delta * 8.0 / seconds;
    }

    private static void Add(List<double> list, double value)
    {
        list.Add(value); if (list.Count > 14400) list.RemoveAt(0);
    }
}

