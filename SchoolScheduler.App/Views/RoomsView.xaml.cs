using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;
namespace SchoolScheduler.App.Views;
public partial class RoomsView : UserControl
{
    public RoomsView() { InitializeComponent(); Loaded += OnLoaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is RoomsViewModel vm && vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null); }
}
