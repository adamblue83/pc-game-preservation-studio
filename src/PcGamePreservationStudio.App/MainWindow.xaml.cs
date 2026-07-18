using System.Windows;
using PcGamePreservationStudio.App.ViewModels;

namespace PcGamePreservationStudio.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
