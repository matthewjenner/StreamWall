using System.Globalization;

namespace StreamWall.Core.Diagnostics;

/// <summary>Where diagnostic lines go (the app's log file). Lines arrive already redacted.</summary>
public interface IDiagSink
{
    void Write(DiagLevel level, string scope, string message);
}

public enum DiagLevel
{
    Info,
    Warn,
    Error,
}

/// <summary>
/// Every diagnostic line the app writes, as a typed method (the Klakr/Updatr/RingView pattern):
/// the text lives here and nowhere else, and everything passes through the
/// <see cref="LogRedactor"/>. Streams are named by cell index and pseudonym, never by label or URL.
/// </summary>
public sealed class DiagLog(IDiagSink sink, LogRedactor redactor)
{
    public LogRedactor Redactor => redactor;

    public void AppStarted(string version, bool demo) =>
        Emit(DiagLevel.Info, "App", "StreamWall " + version + (demo ? " started (demo)" : " started"));

    public void ConfigMissing() => Emit(DiagLevel.Info, "Config", "no config yet; starting empty");

    public void ConfigLoaded(int streams) => Emit(DiagLevel.Info, "Config", F($"config loaded; {streams} stream(s)"));

    public void ConfigUnreadable(string reason) =>
        Emit(DiagLevel.Error, "Config", "config could not be read (" + reason + "); set aside, starting empty");

    public void ConfigSaved(int streams) => Emit(DiagLevel.Info, "Config", F($"config saved; {streams} stream(s)"));

    public void ConfigSaveFailed(Exception error) => Emit(DiagLevel.Error, "Config", "failed to save config: " + error.Message);

    public void PseudonymsReset() => Emit(DiagLevel.Info, "Config", "pseudonym key replaced; every site and stream has a new name");

    public void UiError(string what, Exception error) => Emit(DiagLevel.Error, "UI", what + ": " + error.Message);

    private void Emit(DiagLevel level, string scope, string message) => sink.Write(level, scope, redactor.Redact(message));

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
