using System;
using System.Threading;
using System.Threading.Tasks;
using TdLib;

namespace TelegramWin.Services
{
    public sealed class TdLibService : IDisposable
    {
        private TdClient? _client;
        private CancellationTokenSource? _cts;

        public event Action<TdApi.UpdateAuthorizationState>? AuthorizationChanged;

        public bool IsStarted => _client != null;

        public async Task StartAsync(int apiId, string apiHash, string databaseDir)
        {
            if (_client != null) return;

            _client = new TdClient();
            _cts = new CancellationTokenSource();

            await _client.ExecuteAsync(new TdApi.SetLogVerbosityLevel { NewVerbosityLevel = 1 });

            await _client.SendAsync(new TdApi.SetTdlibParameters
            {
                DatabaseDirectory = databaseDir,
                UseMessageDatabase = true,
                UseSecretChats = false,
                ApiId = apiId,
                ApiHash = apiHash,
                SystemLanguageCode = "ru",
                DeviceModel = "Windows 10",
                ApplicationVersion = "1.0",
                EnableStorageOptimizer = true
            });

            await _client.SendAsync(new TdApi.CheckDatabaseEncryptionKey { EncryptionKey = Array.Empty<byte>() });

            _ = Task.Run(ReceiveLoop, _cts.Token);
        }

        private async Task ReceiveLoop()
        {
            if (_client == null || _cts == null) return;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var upd = await _client.ReceiveAsync(1.0);
                    if (upd == null) continue;

                    if (upd is TdApi.UpdateAuthorizationState auth)
                        AuthorizationChanged?.Invoke(auth);
                }
            }
            catch (OperationCanceledException) { }
        }

        public Task SendPhoneAsync(string phone)
            => Ensure().SendAsync(new TdApi.SetAuthenticationPhoneNumber { PhoneNumber = phone });

        public Task SendCodeAsync(string code)
            => Ensure().SendAsync(new TdApi.CheckAuthenticationCode { Code = code });

        public Task SendPasswordAsync(string password)
            => Ensure().SendAsync(new TdApi.CheckAuthenticationPassword { Password = password });

        private TdClient Ensure()
            => _client ?? throw new InvalidOperationException("TDLib не запущен");

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _client?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }
        }
    }
}
