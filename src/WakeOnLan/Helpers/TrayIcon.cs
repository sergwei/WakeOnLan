using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WakeOnLan.Helpers;

/// <summary>Icono de la aplicación en la bandeja del sistema (área de notificación).</summary>
internal sealed class TrayIcon : IDisposable
{
    // Límite de NotifyIcon.Text en .NET 5+.
    private const int MaxTextLength = 127;

    private readonly Forms.NotifyIcon _icon;

    public TrayIcon()
    {
        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Wake on LAN",
            Visible = true,
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right)
                Clicked?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? Clicked;

    public string Text
    {
        set => _icon.Text = value.Length > MaxTextLength ? value[..MaxTextLength] : value;
    }

    /// <summary>Muestra una notificación de Windows asociada al icono.</summary>
    public void ShowNotification(string title, string message) =>
        _icon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.None);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (resource is null)
            return Drawing.SystemIcons.Application;

        using var stream = resource.Stream;
        // Pide el tamaño exacto de la bandeja para que Windows no reescale la imagen de 32 px.
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
