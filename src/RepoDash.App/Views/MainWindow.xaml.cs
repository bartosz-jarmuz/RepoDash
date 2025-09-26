using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.ViewModels;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace RepoDash.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        Loaded += async (_, __) => await ((MainViewModel)DataContext).LoadCurrentRootAsync();
    }

    private async void RepoRoot_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyData == Keys.Enter)
        {
            e.Handled = true;
            await ((MainViewModel)DataContext).LoadCurrentRootAsync();
        }
    }
}