using System.Windows;
using WakeOnLan.Helpers;

namespace WakeOnLan.Views;

public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string primaryText)
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        Title = title;
        HeaderText.Text = title;
        MessageText.Text = message;
        PrimaryText.Text = primaryText;
    }

    public static bool Ask(Window owner, string title, string message, string primaryText) =>
        new ConfirmDialog(title, message, primaryText) { Owner = owner }.ShowDialog() == true;

    private void Primary_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
