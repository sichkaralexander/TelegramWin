using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TdLib;

namespace TelegramWin.Services
{
    public sealed class TdLibService : IDisposable
    {
        private readonly TdClient _client;
        private readonly SynchronizationContext? _uiContext;

        private readonly string _statusLogPath;
        private readonly object _logLock = new();

        private int _setParamsStarted = 0;

        public event Action<string>? StatusChanged;
        public event Action<AuthStep>? AuthStepChanged;
        public event Action? Authorized;
        public event Action<TdApi.Update>? UpdateReceivedPublic;

        public bool IsAuthorized { get; private set; }
        private bool _authorizedFired;

        public TdLibService()
        {
            _uiContext = SynchronizationContext.Current;

            _statusLogPath = Path.Combine(AppContext.BaseDirectory, "tdlib_statuslog.txt");
            SafeLog("=== TelegramWin TDLibService START ===");
            SafeLog("BaseDirectory: " + AppContext.BaseDirectory);
            SafeLog("PID: " + Environment.ProcessId);

            _client = new TdClient();
            _client.UpdateReceived += OnUpdateReceived;

            RaiseStatus("TDLib запущен");
            RaiseAuthStep(AuthStep.Internal, "Startup");

            _ = KickstartAuthorizationAsync();
        }

        public Task<TResult> ExecuteAsync<TResult>(TdApi.Function<TResult> function)
            where TResult : TdApi.Object
        {
            return _client.ExecuteAsync(function);
        }

        private async Task KickstartAuthorizationAsync()
        {
            try
            {
                SafeLog("KickstartAuthorizationAsync: calling GetAuthorizationState()");
                var state = await _client.ExecuteAsync(new TdApi.GetAuthorizationState());
                SafeLog("KickstartAuthorizationAsync: state=" + (state?.GetType().Name ?? "null"));

                if (state != null)
                    HandleAuthorizationState_NoAwait(state);
            }
            catch (Exception ex)
            {
                SafeLog("KickstartAuthorizationAsync ERROR: " + ex);
                RaiseStatus("Ошибка старта авторизации: " + ex.Message);
            }
        }

        private void OnUpdateReceived(object? sender, TdApi.Update update)
        {
            try
            {
                if (update == null) return;

                SafeLog("Update: " + update.GetType().Name);
                RaiseUpdate(update);

                if (string.Equals(update.GetType().Name, "UpdateAuthorizationState", StringComparison.Ordinal))
                {
                    var authStateObj = update.GetType()
                        .GetProperty("AuthorizationState", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(update);

                    SafeLog("UpdateAuthorizationState: " + (authStateObj?.GetType().Name ?? "null"));

                    if (authStateObj != null)
                        HandleAuthorizationState_NoAwait(authStateObj);
                }
            }
            catch (Exception ex)
            {
                SafeLog("OnUpdateReceived ERROR: " + ex);
                RaiseStatus("Ошибка TDLib: " + ex.Message);
            }
        }

        private void HandleAuthorizationState_NoAwait(object authStateObj)
        {
            var stateName = authStateObj.GetType().Name;

            switch (stateName)
            {
                case "AuthorizationStateWaitTdlibParameters":
                    RaiseStatus("Инициализация…");
                    RaiseAuthStep(AuthStep.Internal, stateName);
                    StartSetTdlibParametersOnce();
                    break;

                case "AuthorizationStateWaitEncryptionKey":
                    RaiseStatus("Проверка ключа базы…");
                    RaiseAuthStep(AuthStep.Internal, stateName);
                    _ = TrySendDatabaseKeyAsync();
                    break;

                case "AuthorizationStateWaitPhoneNumber":
                    RaiseStatus("Введите номер телефона");
                    RaiseAuthStep(AuthStep.Phone, stateName);
                    break;

                case "AuthorizationStateWaitCode":
                    RaiseStatus("Введите код подтверждения");
                    RaiseAuthStep(AuthStep.Code, stateName);
                    break;

                case "AuthorizationStateWaitPassword":
                    RaiseStatus("Введите пароль 2FA");
                    RaiseAuthStep(AuthStep.Password, stateName);
                    break;

                case "AuthorizationStateReady":
                    IsAuthorized = true;
                    RaiseStatus("Авторизация успешна");
                    RaiseAuthStep(AuthStep.Ready, stateName);
                    FireAuthorizedOnce();
                    break;

                default:
                    RaiseStatus("Состояние авторизации: " + stateName);
                    RaiseAuthStep(AuthStep.Internal, stateName);
                    break;
            }
        }

        private void StartSetTdlibParametersOnce()
        {
            if (Interlocked.CompareExchange(ref _setParamsStarted, 1, 0) != 0)
            {
                SafeLog("SetTdlibParameters: already started (skip)");
                return;
            }

            SafeLog("SetTdlibParameters: START (background)");

            _ = Task.Run(async () =>
            {
                try
                {
                    await SetParametersWithTimeoutAsync(TimeSpan.FromSeconds(25));
                    SafeLog("SetTdlibParameters: DONE (background)");
                    await KickstartAuthorizationAsync();
                }
                catch (Exception ex)
                {
                    SafeLog("SetTdlibParameters BACKGROUND ERROR: " + ex);
                    RaiseStatus("Ошибка setTdlibParameters: " + ex.Message);
                }
            });
        }

        private async Task SetParametersWithTimeoutAsync(TimeSpan timeout)
        {
            var setTask = SetParametersAsync();
            var delayTask = Task.Delay(timeout);

            var finished = await Task.WhenAny(setTask, delayTask);
            if (finished == delayTask)
            {
                SafeLog("SetTdlibParameters TIMEOUT after " + timeout.TotalSeconds + "s");
                RaiseStatus("Инициализация зависла (таймаут). Смотрим лог.");
                return;
            }

            await setTask;
            SafeLog("SetTdlibParameters: OK (returned)");
        }

        private static string GetDbFolderName()
        {
            try
            {
                var user = Environment.UserName;
                if (string.IsNullOrWhiteSpace(user)) user = "default";
                var safe = new string(user.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
                if (string.IsNullOrWhiteSpace(safe)) safe = "default";
                return "db_" + safe;
            }
            catch
            {
                return "db_default";
            }
        }

        private async Task SetParametersAsync()
        {
            var tdlibRoot = Path.Combine(AppContext.BaseDirectory, "tdlib");

            // ✅ Уникальная папка БД на пользователя (устраняет lock td.binlog)
            var dbDir = Path.Combine(tdlibRoot, GetDbFolderName());
            var filesDir = Path.Combine(tdlibRoot, "files");

            Directory.CreateDirectory(dbDir);
            Directory.CreateDirectory(filesDir);

            SafeLog("SetTdlibParameters: dbDir=" + dbDir);
            SafeLog("SetTdlibParameters: filesDir=" + filesDir);

            var cfg = AppConfig.Load();
            SafeLog("Config: ApiId=" + cfg.ApiId);
            SafeLog("Config: SystemLanguageCode=" + cfg.SystemLanguageCode);

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

            if (await TryExecuteFunctionByNameAsync("CheckDatabaseEncryptionKey", ("EncryptionKey", key), ("Key", key)))
            {
                SafeLog("DatabaseKey: CheckDatabaseEncryptionKey OK");
                return;
            }

            if (await TryExecuteFunctionByNameAsync("SetDatabaseEncryptionKey", ("EncryptionKey", key), ("Key", key)))
            {
                SafeLog("DatabaseKey: SetDatabaseEncryptionKey OK");
                return;
            }

            SafeLog("DatabaseKey: function not found, continue");
            RaiseStatus("Функция ключа базы не найдена. Продолжаю…");
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

            if (functionBase == null)
                throw new InvalidOperationException("Не найден Function<T> base type.");

            var resultType = functionBase.GetGenericArguments()[0];

            var exec = typeof(TdClient).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ExecuteAsync" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);

            if (exec == null)
                throw new MissingMethodException("ExecuteAsync<T> не найден.");

            var gm = exec.MakeGenericMethod(resultType);
            var taskObj = gm.Invoke(_client, new object[] { functionObject });

            if (taskObj is Task t)
                await t;
        }

        public Task SendPhoneAsync(string phoneNumber) =>
            _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber { PhoneNumber = phoneNumber?.Trim() ?? "" });

        public Task SendCodeAsync(string code) =>
            _client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code?.Trim() ?? "" });

        public Task SendPasswordAsync(string password) =>
            _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword { Password = password ?? "" });

        private void FireAuthorizedOnce()
        {
            if (_authorizedFired) return;
            _authorizedFired = true;
            SafeLog("AUTHORIZED: fired");
            Authorized?.Invoke();
        }

        private void RaiseUpdate(TdApi.Update update)
        {
            if (_uiContext != null)
                _uiContext.Post(_ => UpdateReceivedPublic?.Invoke(update), null);
            else
                UpdateReceivedPublic?.Invoke(update);
        }

        private void RaiseStatus(string text)
        {
            SafeLog("STATUS: " + text);

            if (_uiContext != null)
                _uiContext.Post(_ => StatusChanged?.Invoke(text), null);
            else
                StatusChanged?.Invoke(text);
        }

        private void RaiseAuthStep(AuthStep step, string stateName)
        {
            SafeLog("AUTH_STATE: " + stateName);

            if (_uiContext != null)
                _uiContext.Post(_ => AuthStepChanged?.Invoke(step), null);
            else
                AuthStepChanged?.Invoke(step);
        }

        private void SafeLog(string line)
        {
            try
            {
                var msg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}";
                lock (_logLock)
                {
                    File.AppendAllText(_statusLogPath, msg, Encoding.UTF8);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                SafeLog("=== TdLibService DISPOSE ===");
                _client.UpdateReceived -= OnUpdateReceived;
                _client.Dispose();
            }
            catch { }
        }
    }

    public enum AuthStep
    {
        Internal = 0,
        Phone = 1,
        Code = 2,
        Password = 3,
        Ready = 4
    }
}
