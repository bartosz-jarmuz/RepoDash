using System;
using System.Drawing;
using System.Windows.Forms;
using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Tray;

public sealed class NotifyIconService : ITrayIconService
{
    private readonly NotifyIcon _notifyIcon = new()
    {
        Icon = SystemIcons.Application,
        Visible = false
    };

    public void Initialize(string tooltip, Action onLeftClick)
    {
        _notifyIcon.Text = tooltip;
        _notifyIcon.Visible = true;
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                onLeftClick();
            }
        };
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
