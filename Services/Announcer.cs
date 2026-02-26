using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;

namespace TelegramWin.Services
{
    /// <summary>
    /// Единый сервис озвучки для скринридеров (JAWS-first).
    /// Особенности:
    /// - Работает через Live Region (AutomationProperties.Name)
    /// - Делает reset Name перед установкой текста, чтобы JAWS повторно озвучивал одинаковые фразы
    /// - Никогда не трогает фокус
    /// - Может дублировать текст в обычный статусный TextBlock (если задан statusName)
    /// </summary>
    public sealed class Announcer
    {
        private readonly FrameworkElement _root;
        private readonly string _liveRegionName;
        private readonly string? _statusName;
        private readonly SynchronizationContext? _uiContext;

        public Announcer(FrameworkElement root, string liveRegionName = "LiveStatus", string? statusName = null)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _liveRegionName = string.IsNullOrWhiteSpace(liveRegionName) ? "LiveStatus" : liveRegionName;
            _statusName = statusName;
            _uiContext = SynchronizationContext.Current;
        }

        public void Say(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            void Work()
            {
                if (_statusName != null && _root.FindName(_statusName) is FrameworkElement statusElement)
                {
                    SetVisualText(statusElement, text);
                }

                if (_root.FindName(_liveRegionName) is not FrameworkElement live)
                    return;

                AutomationProperties.SetName(live, string.Empty);

                var dispatcher = _root.Dispatcher;
                if (dispatcher != null)
                {
                    _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        AutomationProperties.SetName(live, text);
                    }));
                }
                else
                {
                    AutomationProperties.SetName(live, text);
                }
            }

            if (_uiContext != null)
            {
                _uiContext.Post(_ => Work(), null);
                return;
            }

            if (_root.Dispatcher != null && !_root.Dispatcher.CheckAccess())
            {
                _ = _root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Work));
                return;
            }

            Work();
        }

        private static void SetVisualText(FrameworkElement element, string text)
        {
            switch (element)
            {
                case System.Windows.Controls.TextBlock tb:
                    tb.Text = text;
                    break;
                case System.Windows.Controls.ContentControl cc:
                    cc.Content = text;
                    break;
            }
        }
    }
}
