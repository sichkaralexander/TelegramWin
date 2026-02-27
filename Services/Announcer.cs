using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
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
        private int _announceCounter;

        public Announcer(FrameworkElement root)
        {
            _root = root;
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

                var sequence = Interlocked.Increment(ref _announceCounter);
                var finalText = $"{text} #{sequence}";

                // Сначала очистка — чтобы JAWS увидел изменение даже при одинаковых фразах.
                SetLiveRegionText(live, string.Empty);

                var timer = new DispatcherTimer
                {
                    Dispatcher = _root.Dispatcher,
                    Interval = TimeSpan.FromMilliseconds(35)
                };

                EventHandler? tick = null;
                tick = (_, _) =>
                {
                    timer.Stop();
                    timer.Tick -= tick;

                    SetLiveRegionText(live, finalText);
                    RaiseLiveRegionChanged(live);
                };

                timer.Tick += tick;
                timer.Start();
            }

            if (_root.Dispatcher.CheckAccess())
                Work();
            else
                _root.Dispatcher.BeginInvoke((Action)Work, DispatcherPriority.Background);
        }

        private static void SetLiveRegionText(FrameworkElement live, string value)
        {
            AutomationProperties.SetName(live, value);

            if (live is TextBlock textBlock)
                textBlock.Text = value;
        }

        private static void RaiseLiveRegionChanged(FrameworkElement live)
        {
            var peer = UIElementAutomationPeer.FromElement(live)
                       ?? UIElementAutomationPeer.CreatePeerForElement(live);

            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
