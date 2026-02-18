using System;
using System.IO;
using System.Text.Json;

namespace TelegramWin.Services
{
    public sealed class AppConfig
    {
        public int ApiId { get; set; } = 0;
        public string ApiHash { get; set; } = "PASTE_API_HASH_HERE";
        public string DataDirectory { get; set; } = "tdlib";

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static AppConfig Load()
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
    }
}
