using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;
namespace SchoolScheduler.App.Views;
public partial class SchoolView : UserControl
{
    public SchoolView() { InitializeComponent(); Loaded += OnLoaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is SchoolViewModel vm) vm.LoadCommand.Execute(null); }
}
