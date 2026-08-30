using Newtonsoft.Json;
using RulerOverlay.Models;
using System;
using System.IO;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Loads and saves ruler configuration as JSON.
    /// Config file location: %APPDATA%\RulerOverlay\config.json
    /// </summary>
    public class ConfigurationService
    {
        private const string ConfigFileName = "config.json";
        private const string AppFolderName = "RulerOverlay";

        private readonly string _configPath;
        private readonly object _fileLock = new();

        /// <summary>
        /// Shortcuts read from disk at load time. They are not editable in the UI, but
        /// they are carried through every save so hand-edits to config.json are not
        /// silently reset by the next automatic save.
        /// </summary>
        private System.Collections.Generic.Dictionary<string, string>? _preservedShortcuts;

        public ConfigurationService()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);

            _configPath = Path.Combine(appDataFolder, ConfigFileName);

            try
            {
                Directory.CreateDirectory(appDataFolder);
            }
            catch (Exception ex)
            {
                // A read-only or redirected profile means settings cannot persist.
                // The app still runs; Save() will simply keep failing harmlessly.
                System.Diagnostics.Debug.WriteLine(
                    $"[ConfigurationService] Cannot create '{appDataFolder}': {ex.Message}");
            }
        }

        /// <summary>Full path to the configuration file.</summary>
        public string ConfigPath => _configPath;

        /// <summary>
        /// True when <see cref="Load"/> found no existing config file, i.e. this is the
        /// first run. Callers use it to apply first-run behaviour such as centring the
        /// ruler instead of restoring a position that was never saved.
        /// </summary>
        public bool IsFirstRun { get; private set; }

        /// <summary>
        /// Loads and validates the configuration.
        /// Returns sanitized defaults if the file is missing, unreadable or corrupt.
        /// </summary>
        public RulerConfig Load()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_configPath))
                    {
                        IsFirstRun = true;
                        var defaultConfig = RulerConfig.Default;
                        _preservedShortcuts = defaultConfig.Shortcuts;
                        SaveInternal(defaultConfig);
                        return defaultConfig;
                    }

                    var json = File.ReadAllText(_configPath);
                    var loaded = JsonConvert.DeserializeObject<RulerConfig>(json) ?? RulerConfig.Default;

                    // Sanitize before use: missing fields from an older version fall back
                    // to defaults, and out-of-range values are clamped.
                    var sanitized = loaded.Sanitized();
                    _preservedShortcuts = sanitized.Shortcuts;
                    return sanitized;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ConfigurationService] Failed to load config: {ex.Message}");
                    return RulerConfig.Default;
                }
            }
        }

        /// <summary>
        /// Saves the configuration, preserving any shortcut map that was read at load time.
        /// </summary>
        public void Save(RulerConfig config)
        {
            if (config == null)
                return;

            lock (_fileLock)
            {
                if (_preservedShortcuts is { Count: > 0 })
                    config.Shortcuts = _preservedShortcuts;

                SaveInternal(config);
            }
        }

        /// <summary>
        /// Loads the configuration, applies an update, and saves it back.
        /// </summary>
        public void Update(Action<RulerConfig> updateAction)
        {
            ArgumentNullException.ThrowIfNull(updateAction);

            var config = Load();
            updateAction(config);
            Save(config);
        }

        /// <summary>
        /// Writes to a temporary file and then replaces the real one, so an interrupted
        /// write cannot leave a truncated config.json behind. Caller holds <see cref="_fileLock"/>.
        /// </summary>
        private void SaveInternal(RulerConfig config)
        {
            var tempPath = _configPath + ".tmp";

            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configPath, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ConfigurationService] Failed to save config: {ex.Message}");

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ConfigurationService] Failed to remove temp file: {cleanupEx.Message}");
                }
            }
        }
    }
}
