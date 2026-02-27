using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace TelegramWin.Services
{
    /// <summary>
    /// Озвучка статусов для JAWS через Live Region.
    /// Работает так:
    /// - В окне должен быть элемент (обычно TextBlock) с x:Name="LiveStatus"
    /// - Мы меняем AutomationProperties.Name
    /// - Принудительно вызываем LiveRegionChanged
    /// </summary>
    public sealed class Announcer
    {
        private readonly FrameworkElement _root;
        private readonly SynchronizationContext? _uiContext;

        public Announcer(FrameworkElement root)
        {
            _root = root;
            _uiContext = SynchronizationContext.Current;
        }

        public void Say(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            void Work()
            {
                var live = _root.FindName("LiveStatus") as FrameworkElement;
                if (live == null)
                    return;

                // Сначала очистка — чтобы JAWS видел изменение даже для одинаковых фраз
                AutomationProperties.SetName(live, "");

                var timer = new DispatcherTimer(DispatcherPriority.Background, _root.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(35)
                };

                EventHandler? tick = null;
                tick = (_, _) =>
                {
                    timer.Stop();
                    timer.Tick -= tick;

                    AutomationProperties.SetName(live, text);
                    RaiseLiveRegionChanged(live);
                };

                timer.Tick += tick;
                timer.Start();
            }

            if (_uiContext != null)
                _uiContext.Post(_ => Work(), null);
            else
                Work();
        }

        private static void RaiseLiveRegionChanged(FrameworkElement live)
        {
            var peer = UIElementAutomationPeer.FromElement(live)
                       ?? UIElementAutomationPeer.CreatePeerForElement(live);

            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
