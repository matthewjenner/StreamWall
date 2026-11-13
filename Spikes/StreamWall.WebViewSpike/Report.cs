using System.Diagnostics;
using System.Text;

namespace StreamWall.WebViewSpike;

/// <summary>Collects the run's elapsed time and writes the human-readable summary.</summary>
internal sealed class Report
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly StringBuilder _text = new();

    public Report(string dir)
    {
        Dir = dir;
        Directory.CreateDirectory(dir);
    }

    public string Dir { get; }
    public double Now => _clock.Elapsed.TotalSeconds;

    public void Line(string s = "") => _text.AppendLine(s);

    public string Save(string name)
    {
        string path = Path.Combine(Dir, name);
        File.WriteAllText(path, _text.ToString());
        return path;
    }
}
