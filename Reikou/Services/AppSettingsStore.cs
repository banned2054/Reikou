using Reikou.Models.Settings;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

public sealed class AppSettingsStore
{
    private const string ApplicationDirectoryName = "Reikou";
    private const string SettingsFileName         = "settings.json";

    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private          AppSettings   _settings;

    public static AppSettingsStore Current { get; } = Load(GetDefaultSettingsPath());

    public string      SettingsPath { get; }
    public AppSettings Settings     => Volatile.Read(ref _settings);

    private AppSettingsStore(string settingsPath, AppSettings settings)
    {
        SettingsPath = settingsPath;
        _settings    = settings.Normalize();
    }

    public AppSettings Update(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        Volatile.Write(ref _settings, normalized);
        return normalized;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;

        try
        {
            var settings = Settings;
            var directory = Path.GetDirectoryName(SettingsPath)
                         ?? throw new InvalidOperationException("Settings path has no parent directory.");
            Directory.CreateDirectory(directory);

            temporaryPath = Path.Combine(directory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                                     FileShare.None, 4096,
                                                     FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer
                     .SerializeAsync(stream, settings, AppSettingsJsonContext.Default.AppSettings, cancellationToken)
                     .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk : true);
            }

            ReplaceSettingsFile(temporaryPath, SettingsPath);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath != null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException exception)
                {
                    Debug.WriteLine($"Failed to remove temporary settings file: {exception}");
                }
            }

            _saveGate.Release();
        }
    }

    private static AppSettingsStore Load(string settingsPath)
    {
        if (TryLoadSettings(settingsPath, out var settings))
        {
            return new AppSettingsStore(settingsPath, settings);
        }

        var backupPath = GetBackupPath(settingsPath);
        return TryLoadSettings(backupPath, out settings)
            ? new AppSettingsStore(settingsPath, settings)
            : new AppSettingsStore(settingsPath, new AppSettings());
    }

    private static bool TryLoadSettings(string path, out AppSettings settings)
    {
        settings = new AppSettings();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var       loaded = JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings);
            if (loaded == null)
            {
                return false;
            }

            settings = loaded.Normalize();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Failed to load settings from '{path}': {exception}");
            return false;
        }
    }

    private static void ReplaceSettingsFile(string temporaryPath, string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            File.Move(temporaryPath, settingsPath);
            return;
        }

        var backupPath = GetBackupPath(settingsPath);
        try
        {
            File.Replace(temporaryPath, settingsPath, backupPath, ignoreMetadataErrors : true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(settingsPath, backupPath, overwrite : true);
            File.Move(temporaryPath, settingsPath, overwrite : true);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, ApplicationDirectoryName, SettingsFileName);
    }

    private static string GetBackupPath(string settingsPath) => $"{settingsPath}.bak";
}
