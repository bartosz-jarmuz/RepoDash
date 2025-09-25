using System.Threading.Tasks;
using System.Windows;
using RepoDash.App.ViewModels;

namespace RepoDash.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        SearchBox.Focus();
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
        SearchBox.Focus();
    }
}
