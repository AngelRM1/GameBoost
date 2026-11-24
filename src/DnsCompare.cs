using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameBoost
{
    /// <summary>
    /// Comparador de DNS (v1.8.0): pregunta directamente a cada servidor
    /// DNS con una consulta UDP real (puerto 53) y mide cuanto tarda en
    /// responder. No cambia nada del sistema: solo mide.
    /// </summary>
    public record DnsProvider(string Name, string Primary, string Secondary, bool IsSystem);

    public record DnsResult(string Name, string Primary, long? BestMs, long? AvgMs)
    {
        public string Display()
        {
            if (BestMs == null) return $"{Name} ({Primary}): sin respuesta";
            return $"{Name} ({Primary}): {BestMs} ms";
        }
    }

    public static class DnsCompare
    {
        public static readonly List<DnsProvider> Providers = new()
        {
            new DnsProvider("Cloudflare", "1.1.1.1", "1.0.0.1", false),
            new DnsProvider("Google", "8.8.8.8", "8.8.4.4", false),
            new DnsProvider("Quad9", "9.9.9.9", "149.112.112.112", false),
            new DnsProvider("OpenDNS", "208.67.222.222", "208.67.220.220", false),
            new DnsProvider("DNS actual (tu ISP)", "", "", true),
        };

        public const string TestHost = "www.google.com";

        /// <summary>Construye una consulta DNS estandar (A, IN) para un nombre.</summary>
        public static byte[] BuildQuery(ushort id, string host)
        {
            using var ms = new System.IO.MemoryStream();
            ms.WriteByte((byte)(id >> 8)); ms.WriteByte((byte)id);
            ms.WriteByte(0x01); ms.WriteByte(0x00); // Flags: recursion deseada
            ms.WriteByte(0); ms.WriteByte(1);       // QDCOUNT = 1
            ms.WriteByte(0); ms.WriteByte(0);       // ANCOUNT = 0
            ms.WriteByte(0); ms.WriteByte(0);       // NSCOUNT = 0
            ms.WriteByte(0); ms.WriteByte(0);       // ARCOUNT = 0
            foreach (var label in host.Split('.'))
            {
                ms.WriteByte((byte)label.Length);
                ms.Write(Encoding.ASCII.GetBytes(label), 0, label.Length);
            }
            ms.WriteByte(0);                        // Fin del nombre
            ms.WriteByte(0); ms.WriteByte(1);       // QTYPE = A
            ms.WriteByte(0); ms.WriteByte(1);       // QCLASS = IN
            return ms.ToArray();
        }

        /// <summary>Valida que una respuesta DNS corresponda a nuestra consulta y no tenga error.</summary>
        public static bool IsValidResponse(byte[] response, ushort id)
        {
            if (response == null || response.Length < 12) return false;
            ushort respId = (ushort)((response[0] << 8) | response[1]);
            if (respId != id) return false;
            int rcode = response[3] & 0x0F;
            return rcode == 0;
        }

        /// <summary>Una medicion contra un servidor concreto. Null si no responde a tiempo.</summary>
        public static async Task<long?> ProbeOnceAsync(string serverIp, string host, int timeoutMs = 2000, int port = 53)
        {
            ushort id = (ushort)new Random().Next(1, 65535);
            var query = BuildQuery(id, host);
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = new UdpClient();
                client.Connect(serverIp, port);
                await client.SendAsync(query, query.Length);
                using var cts = new CancellationTokenSource(timeoutMs);
                var result = await client.ReceiveAsync(cts.Token);
                sw.Stop();
                return IsValidResponse(result.Buffer, id) ? sw.ElapsedMilliseconds : null;
            }
            catch { sw.Stop(); return null; }
        }

        /// <summary>Una medicion con el DNS del sistema (el que usa Windows ahora).</summary>
        public static async Task<long?> ProbeSystemOnceAsync(string host, int timeoutMs = 2000)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                var entry = await Dns.GetHostEntryAsync(host, cts.Token);
                sw.Stop();
                return entry.AddressList.Length > 0 ? sw.ElapsedMilliseconds : null;
            }
            catch { sw.Stop(); return null; }
        }

        /// <summary>Mide un proveedor varias veces y devuelve su mejor y su media.</summary>
        public static async Task<DnsResult> ProbeProviderAsync(DnsProvider p, int tries = 3, int timeoutMs = 2000, int port = 53)
        {
            var times = new List<long>();
            for (int i = 0; i < tries; i++)
            {
                long? ms = p.IsSystem
                    ? await ProbeSystemOnceAsync(TestHost, timeoutMs)
                    : await ProbeOnceAsync(p.Primary, TestHost, timeoutMs, port);
                if (ms.HasValue) times.Add(ms.Value);
            }
            if (times.Count == 0) return new DnsResult(p.Name, p.IsSystem ? "sistema" : p.Primary, null, null);
            return new DnsResult(p.Name, p.IsSystem ? "sistema" : p.Primary, times.Min(), (long)times.Average());
        }

        /// <summary>Compara todos los proveedores, del mas rapido al mas lento.</summary>
        public static async Task<List<DnsResult>> CompareAllAsync(Action<string>? progress = null)
        {
            var results = new List<DnsResult>();
            for (int i = 0; i < Providers.Count; i++)
            {
                progress?.Invoke($"Comparando... {i + 1}/{Providers.Count}: {Providers[i].Name}");
                results.Add(await ProbeProviderAsync(Providers[i]));
            }
            return results
                .OrderBy(r => r.BestMs ?? long.MaxValue)
                .ToList();
        }
    }
}
