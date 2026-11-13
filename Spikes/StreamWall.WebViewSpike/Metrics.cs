using System.Diagnostics;
using System.Runtime.Versioning;

namespace StreamWall.WebViewSpike;

internal sealed record ProcSample(double T, double CpuCores, double PrivateMb, double Gpu3d, double GpuDecode, int Processes);

/// <summary>Samples CPU, private memory and GPU engine use for the app and its WebView2 processes.</summary>
[SupportedOSPlatform("windows")]
internal sealed class Metrics
{
    private readonly Dictionary<int, TimeSpan> _lastCpu = [];
    private readonly Dictionary<string, PerformanceCounter> _gpu = [];
    private DateTime _lastWall = DateTime.UtcNow;

    public List<ProcSample> Samples { get; } = [];

    public void Sample(double t, IReadOnlyCollection<int> pids)
    {
        var now = DateTime.UtcNow;
        double wall = (now - _lastWall).TotalSeconds;
        _lastWall = now;

        double cpu = 0, priv = 0;
        int alive = 0;
        foreach (int pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                var total = p.TotalProcessorTime;
                if (_lastCpu.TryGetValue(pid, out var prev) && wall > 0) cpu += (total - prev).TotalSeconds / wall;
                _lastCpu[pid] = total;
                priv += p.PrivateMemorySize64 / (1024.0 * 1024.0);
                alive++;
            }
            catch (ArgumentException) { /* exited */ }
            catch (InvalidOperationException) { /* exited */ }
        }

        double g3d = 0, gdec = 0;
        try
        {
            var cat = new PerformanceCounterCategory("GPU Engine");
            var names = cat.GetInstanceNames();
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                if (!pids.Any(pid => name.StartsWith($"pid_{pid}_", StringComparison.Ordinal))) continue;
                if (!name.EndsWith("engtype_3D", StringComparison.Ordinal) &&
                    !name.EndsWith("engtype_VideoDecode", StringComparison.Ordinal)) continue;
                wanted.Add(name);
                if (!_gpu.TryGetValue(name, out var c))
                {
                    c = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, readOnly: true);
                    c.NextValue();
                    _gpu[name] = c;
                    continue;
                }
                float v = c.NextValue();
                if (name.EndsWith("engtype_3D", StringComparison.Ordinal)) g3d += v; else gdec += v;
            }
            foreach (string stale in _gpu.Keys.Where(k => !wanted.Contains(k)).ToList())
            {
                _gpu[stale].Dispose();
                _gpu.Remove(stale);
            }
        }
        catch (Exception)
        {
            g3d = gdec = double.NaN;
        }

        lock (Samples) Samples.Add(new ProcSample(t, cpu, priv, g3d, gdec, alive));
    }
}
