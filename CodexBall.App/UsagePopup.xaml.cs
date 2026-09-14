using System.Windows;
using System.Windows.Input;

namespace CodexBall.App;

public partial class UsagePopup : Window
{
    public UsagePopup()
    {
        InitializeComponent();
        Deactivated += (_, _) => Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Close();
        base.OnMouseLeftButtonDown(e);
    }
}
