using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TdLib;

namespace TelegramWin.Services
{
    public sealed class TdLibService : IDisposable
    {
        private readonly TdClient _client;
        private readonly SynchronizationContext? _uiContext;

        public event Action<string>? StatusChanged;
        public event Action<AuthStep>? AuthStepChanged;
        public event Action? Authorized;

        public bool IsAuthorized { get; private set; }

        public TdLibService()
        {
            _uiContext = SynchronizationContext.Current;
            _client = new TdClient();
            _client.UpdateReceived += OnUpdateReceived;
            RaiseStatus("TDLib запущен");
        }

        private void OnUpdateReceived(object? sender, TdApi.Update update)
        {
            try
            {
                if (update == null) return;
                if (string.Equals(update.GetType().Name, "UpdateAuthorizationState", StringComparison.OrdinalIgnoreCase))
                {
                    var authStateObj = update.GetType().GetProperty("AuthorizationState")?.GetValue(update);
                    if (authStateObj != null) _ = HandleAuthorizationStateAsync(authStateObj);
                }
            }
            catch (Exception ex)
            {
                RaiseStatus("Ошибка TDLib: " + ex.Message);
            }
        }

        private async Task HandleAuthorizationStateAsync(object authStateObj)
        {
            var stateName = authStateObj.GetType().Name;

            switch (stateName)
            {
                case "AuthorizationStateWaitTdlibParameters":
                    RaiseStatus("Инициализация…");
                    AuthStepChanged?.Invoke(AuthStep.Internal);
                    await SetParametersAsync();
                    break;

                case "AuthorizationStateWaitEncryptionKey":
                    RaiseStatus("Проверка ключа базы…");
                    AuthStepChanged?.Invoke(AuthStep.Internal);
                    await TrySendDatabaseKeyAsync();
                    break;

                case "AuthorizationStateWaitPhoneNumber":
                    RaiseStatus("Введите номер телефона");
                    AuthStepChanged?.Invoke(AuthStep.Phone);
                    break;

                case "AuthorizationStateWaitCode":
                    RaiseStatus("Введите код подтверждения");
                    AuthStepChanged?.Invoke(AuthStep.Code);
                    break;

                case "AuthorizationStateWaitPassword":
                    RaiseStatus("Введите пароль 2FA");
                    AuthStepChanged?.Invoke(AuthStep.Password);
                    break;

                case "AuthorizationStateReady":
                    IsAuthorized = true;
                    RaiseStatus("Авторизация успешна");
                    AuthStepChanged?.Invoke(AuthStep.Ready);
                    Authorized?.Invoke();
                    break;

                default:
                    RaiseStatus("Состояние авторизации: " + stateName);
                    break;
            }
        }

        private async Task SetParametersAsync()
        {
            var dbDir = Path.Combine(AppContext.BaseDirectory, "tdlib", "db");
            var filesDir = Path.Combine(AppContext.BaseDirectory, "tdlib", "files");
            Directory.CreateDirectory(dbDir);
            Directory.CreateDirectory(filesDir);

            var cfg = AppConfig.Load();

            await _client.ExecuteAsync(new TdApi.SetTdlibParameters
            {
                UseTestDc = false,
                DatabaseDirectory = dbDir,
                FilesDirectory = filesDir,
                DatabaseEncryptionKey = Array.Empty<byte>(),

                UseFileDatabase = true,
                UseChatInfoDatabase = true,
                UseMessageDatabase = true,
                UseSecretChats = false,

                ApiId = cfg.ApiId,
                ApiHash = cfg.ApiHash,

                SystemLanguageCode = cfg.SystemLanguageCode,
                DeviceModel = cfg.DeviceModel,
                SystemVersion = cfg.SystemVersion,
                ApplicationVersion = cfg.ApplicationVersion
            });
        }

        private async Task TrySendDatabaseKeyAsync()
        {
            var key = Array.Empty<byte>();
            if (await TryExecuteFunctionByNameAsync("CheckDatabaseEncryptionKey", ("EncryptionKey", key), ("Key", key))) return;
            if (await TryExecuteFunctionByNameAsync("SetDatabaseEncryptionKey", ("EncryptionKey", key), ("Key", key))) return;
            RaiseStatus("Функция ключа базы в TdApi не найдена. Продолжаю.");
        }

        private async Task<bool> TryExecuteFunctionByNameAsync(string typeShortName, params (string prop, object value)[] tryProps)
        {
            var asm = typeof(TdApi).Assembly;
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeShortName);
            if (t == null) return false;

            var obj = Activator.CreateInstance(t);
            if (obj == null) return false;

            foreach (var (propName, value) in tryProps)
            {
                var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value);
                    break;
                }
            }

            await ExecuteAnyFunctionAsync(obj);
            return true;
        }

        private async Task ExecuteAnyFunctionAsync(object functionObject)
        {
            // Найти base Function<T>
            Type? cur = functionObject.GetType();
            Type? functionBase = null;
            while (cur != null)
            {
                if (cur.IsGenericType && cur.GetGenericTypeDefinition().Name == "Function`1")
                {
                    functionBase = cur;
                    break;
                }
                cur = cur.BaseType;
            }
            if (functionBase == null) throw new InvalidOperationException("Не найден Function<T> base type.");

            var resultType = functionBase.GetGenericArguments()[0];

            var exec = typeof(TdClient).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ExecuteAsync" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (exec == null) throw new MissingMethodException("ExecuteAsync<T> not found.");

            var gm = exec.MakeGenericMethod(resultType);
            var taskObj = gm.Invoke(_client, new object[] { functionObject });
            if (taskObj is Task t) await t;
        }

        public Task SendPhoneAsync(string phoneNumber) =>
            _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber { PhoneNumber = phoneNumber?.Trim() ?? "" });

        public Task SendCodeAsync(string code) =>
            _client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code?.Trim() ?? "" });

        public Task SendPasswordAsync(string password) =>
            _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword { Password = password ?? "" });

        private void RaiseStatus(string text)
        {
            if (_uiContext != null) _uiContext.Post(_ => StatusChanged?.Invoke(text), null);
            else StatusChanged?.Invoke(text);
        }

        public void Dispose()
        {
            try { _client.UpdateReceived -= OnUpdateReceived; _client.Dispose(); } catch { }
        }
    }

    public enum AuthStep { Internal=0, Phone=1, Code=2, Password=3, Ready=4 }
}
