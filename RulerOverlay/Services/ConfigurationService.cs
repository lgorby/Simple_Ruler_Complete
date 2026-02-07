using Newtonsoft.Json;
using RulerOverlay.Models;
using System;
using System.IO;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Service for loading and saving ruler configuration to/from JSON file
    /// Config file location: %APPDATA%\RulerOverlay\config.json
    /// </summary>
    public class ConfigurationService
    {
        private readonly string _configPath;
        private readonly string _appDataFolder;
        private const string CONFIG_FILENAME = "config.json";
        private const string APP_FOLDER_NAME = "RulerOverlay";

        public ConfigurationService()
        {
            _appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                APP_FOLDER_NAME
            );

            // Ensure app data folder exists
            Directory.CreateDirectory(_appDataFolder);

            _configPath = Path.Combine(_appDataFolder, CONFIG_FILENAME);
        }

        /// <summary>
        /// Loads configuration from JSON file
        /// Returns default configuration if file doesn't exist or cannot be loaded
        /// </summary>
        public RulerConfig Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    // Create default config file on first run
                    var defaultConfig = RulerConfig.Default;
                    Save(defaultConfig);
                    return defaultConfig;
                }

                var json = File.ReadAllText(_configPath);
                var loaded = JsonConvert.DeserializeObject<RulerConfig>(json);

                // Merge with defaults to handle missing fields from older versions
                return MergeWithDefaults(loaded ?? RulerConfig.Default);
            }
            catch (Exception ex)
            {
                // Log error and return defaults
                System.Diagnostics.Debug.WriteLine($"[ConfigurationService] Failed to load config: {ex.Message}");
                return RulerConfig.Default;
            }
        }

        /// <summary>
        /// Saves configuration to JSON file with formatted indentation
        /// </summary>
        public void Save(RulerConfig config)
        {
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigurationService] Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates configuration by applying a partial update action
        /// </summary>
        public void Update(Action<RulerConfig> updateAction)
        {
            var config = Load();
            updateAction(config);
            Save(config);
        }

        /// <summary>
        /// Merges loaded config with defaults to fill in missing fields
        /// </summary>
        private RulerConfig MergeWithDefaults(RulerConfig loaded)
        {
            var defaults = RulerConfig.Default;

            return new RulerConfig
            {
                Position = loaded.Position ?? defaults.Position,
                Size = loaded.Size ?? defaults.Size,
                Rotation = loaded.Rotation,
                Unit = string.IsNullOrEmpty(loaded.Unit) ? defaults.Unit : loaded.Unit,
                Opacity = loaded.Opacity > 0 ? loaded.Opacity : defaults.Opacity,
                Color = string.IsNullOrEmpty(loaded.Color) ? defaults.Color : loaded.Color,
                Ppi = loaded.Ppi > 0 ? loaded.Ppi : defaults.Ppi,
                MagnifierZoom = loaded.MagnifierZoom > 0 ? loaded.MagnifierZoom : defaults.MagnifierZoom,
                MagnifierEnabled = loaded.MagnifierEnabled,
                EdgeSnappingEnabled = loaded.EdgeSnappingEnabled,
                Shortcuts = loaded.Shortcuts ?? defaults.Shortcuts
            };
        }

        /// <summary>
        /// Gets the full path to the configuration file
        /// </summary>
        public string GetConfigPath() => _configPath;
    }
}
