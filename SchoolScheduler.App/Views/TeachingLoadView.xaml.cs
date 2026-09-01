using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;
namespace SchoolScheduler.App.Views;
public partial class TeachingLoadView : UserControl
{
    public TeachingLoadView() { InitializeComponent(); Loaded += OnLoaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is TeachingLoadViewModel vm && vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null); }
}
