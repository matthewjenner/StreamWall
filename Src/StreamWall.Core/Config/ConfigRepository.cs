using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Config;

/// <summary>Where the config's encrypted bytes live (the Platform store, or memory in demo/tests).</summary>
public interface IConfigStore
{
    /// <summary>The stored JSON, or null if there is none yet.</summary>
    /// <exception cref="ConfigStoreException">It exists but cannot be read; the store has set it aside.</exception>
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>Moves an unusable stored config aside so the next save does not destroy it.</summary>
    Task QuarantineAsync(CancellationToken cancellationToken = default);
}

/// <summary>A stored config that exists but cannot be read. The message never quotes content.</summary>
public sealed class ConfigStoreException(string reason) : Exception(reason);

/// <summary>
/// Loads and saves the config, and keeps the log redactor in step with it: every save or load
/// re-registers every URL and the pseudonym key, so a URL the user just typed is redacted before
/// anything can log it.
/// </summary>
public sealed class ConfigRepository(IConfigStore store, DiagLog log)
{
    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppConfig config;
        string? json;
        try
        {
            json = await store.LoadAsync(cancellationToken);
        }
        catch (ConfigStoreException ex)
        {
            log.ConfigUnreadable(ex.Message);
            return await StartFreshAsync(cancellationToken);
        }

        if (json is null)
        {
            log.ConfigMissing();
            return await StartFreshAsync(cancellationToken);
        }

        if (!ConfigDocument.TryParse(json, out config, out string? problem))
        {
            await store.QuarantineAsync(cancellationToken);
            log.ConfigUnreadable(problem ?? "unknown");
            return await StartFreshAsync(cancellationToken);
        }

        Apply(config);
        log.ConfigLoaded(config.Streams.Count);
        return config;
    }

    public async Task<AppConfig> SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        AppConfig normalized = ConfigRules.Normalize(config);
        // Register before writing: nothing about this config may reach a log unredacted.
        Apply(normalized);
        try
        {
            await store.SaveAsync(ConfigDocument.Serialize(normalized), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.ConfigSaveFailed(ex);
            throw;
        }
        log.ConfigSaved(normalized.Streams.Count);
        return normalized;
    }

    /// <summary>New key, new names for every site and stream.</summary>
    public async Task<AppConfig> ResetPseudonymsAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        AppConfig saved = await SaveAsync(config with { PseudonymKey = "" }, cancellationToken);
        log.PseudonymsReset();
        return saved;
    }

    private async Task<AppConfig> StartFreshAsync(CancellationToken cancellationToken)
    {
        AppConfig fresh = ConfigRules.Normalize(new AppConfig());
        Apply(fresh);
        try
        {
            return await SaveAsync(fresh, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Already logged; run with the in-memory config rather than fail to start.
            return fresh;
        }
    }

    private void Apply(AppConfig config) => log.Redactor.Reset(ConfigRules.PseudonymizerFor(config), config.Urls());
}
