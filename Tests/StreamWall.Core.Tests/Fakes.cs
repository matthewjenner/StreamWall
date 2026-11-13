using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Tests;

internal sealed class ListSink : IDiagSink
{
    public List<string> Lines { get; } = [];

    public void Write(DiagLevel level, string scope, string message) => Lines.Add($"[{level}] {scope}: {message}");

    public string All => string.Join("\n", Lines);
}

internal sealed class FakeConfigStore : IConfigStore
{
    public string? Json { get; set; }
    public string? ThrowOnLoad { get; set; }
    public bool FailSave { get; set; }
    public int Quarantined { get; private set; }
    public int Saves { get; private set; }

    public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
        ThrowOnLoad is { } reason ? throw new ConfigStoreException(reason) : Task.FromResult(Json);

    public Task SaveAsync(string json, CancellationToken cancellationToken = default)
    {
        if (FailSave)
            throw new IOException("disk full writing https://cams.example.invalid/view/1");
        Json = json;
        Saves++;
        return Task.CompletedTask;
    }

    public Task QuarantineAsync(CancellationToken cancellationToken = default)
    {
        Quarantined++;
        Json = null;
        return Task.CompletedTask;
    }
}
