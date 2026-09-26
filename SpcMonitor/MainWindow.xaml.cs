using System.Windows;
using SpcMonitor.ViewModels;

namespace SpcMonitor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
