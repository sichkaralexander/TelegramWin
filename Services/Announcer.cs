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
    /// - В окне должен быть элемент (обычно TextBlock) с AutomationId="LiveStatus"
    /// - Мы меняем AutomationProperties.Name, и скринридер озвучивает.
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
            if (string.IsNullOrWhiteSpace(text)) return;

            void Work()
            {
                var live = _root.FindName("LiveStatus") as FrameworkElement;
                if (live == null) return;

                // Трюк для повторных фраз: сначала очистить, потом выставить.
                AutomationProperties.SetName(live, "");

                // Небольшая задержка нужна, чтобы JAWS успел увидеть изменение.
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(35),
                    Priority = DispatcherPriority.Background
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
            var peer = UIElementAutomationPeer.FromElement(live) ?? UIElementAutomationPeer.CreatePeerForElement(live);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
