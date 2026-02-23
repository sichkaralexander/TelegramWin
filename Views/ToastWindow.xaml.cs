using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TelegramWin.Views
{
    public partial class ToastWindow : Window
    {
        private readonly DispatcherTimer _timer;

        public ToastWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            _timer.Tick += (_, __) =>
            {
                _timer.Stop();
                try { Hide(); } catch { }
            };
        }

        public void ShowToast(string message)
        {
            Text.Text = message;

            PositionBottomRight();

            try
            {
                if (!IsVisible)
                    Show();
                else
                    Visibility = Visibility.Visible;

                _timer.Stop();
                _timer.Start();

                // На всякий случай — показ без активации
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                ex |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            }
            catch { }
        }

        private void PositionBottomRight()
        {
            try
            {
                var wa = SystemParameters.WorkArea;
                Left = wa.Right - Width - 16;
                Top = wa.Bottom - Height - 16;
            }
            catch { }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int SW_SHOWNOACTIVATE = 4;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
