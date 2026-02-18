using System;
using System.IO;
using System.Text.Json;

namespace TelegramWin.Services
{
    /// <summary>
    /// Простая загрузка настроек из appsettings.json рядом с exe.
    /// Пользователь редактирует файл без кода.
    /// </summary>
    public sealed class AppConfig
    {
        public int ApiId { get; set; } = 0;
        public string ApiHash { get; set; } = "";

        public string SystemLanguageCode { get; set; } = "ru";
        public string DeviceModel { get; set; } = "TelegramWin";
        public string SystemVersion { get; set; } = Environment.OSVersion.VersionString;
        public string ApplicationVersion { get; set; } = "0.1";

        public static AppConfig Load()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(path))
                    return new AppConfig();

                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return cfg ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }
    }
}
