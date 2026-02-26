using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TelegramWin.Services;
using TelegramWin.Views;

namespace TelegramWin
{
    public partial class App : Application
    {
        public static TdLibService TdLib { get; private set; } = null!;
        private static string CrashLogPath => Path.Combine(AppContext.BaseDirectory, "crashlog.txt");

        private Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // ✅ Один экземпляр приложения
            _singleInstanceMutex = new Mutex(true, "TelegramWin_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                WriteCrash("SingleInstance", new InvalidOperationException("Второй экземпляр приложения не разрешён."));
                Shutdown(0);
                return;
            }

            // Глобальные хэндлеры падений
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                try { WriteCrash("AppDomain.UnhandledException", args.ExceptionObject as Exception); } catch { }
            };

            DispatcherUnhandledException += (_, args) =>
            {
                try { WriteCrash("DispatcherUnhandledException", args.Exception); } catch { }
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                try { WriteCrash("TaskScheduler.UnobservedTaskException", args.Exception); } catch { }
                args.SetObserved();
            };

            try
            {
                base.OnStartup(e);

                TdLib = new TdLibService();
                TdLib.Authorized += OnAuthorized;

                var login = new LoginWindow();
                MainWindow = login;
                login.Show();
            }
            catch (Exception ex)
            {
                WriteCrash("OnStartup TRY/CATCH", ex);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }

        private void OnAuthorized()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (Current.Windows.OfType<ChatsWindow>().Any())
                    return;

                var chats = new ChatsWindow(TdLib);
                chats.Show();

                Current.MainWindow = chats;
                chats.Activate();

                foreach (var w in Current.Windows.OfType<LoginWindow>().ToList())
                    w.Close();
            }));
        }

        private static void WriteCrash(string title, Exception? ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} === {title} ===");
            sb.AppendLine(ex != null ? ex.ToString() : "Exception is null");
            sb.AppendLine();
            File.AppendAllText(CrashLogPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
