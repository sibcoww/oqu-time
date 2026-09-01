using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;
namespace SchoolScheduler.App.Views;
public partial class ConstraintsView : UserControl
{
    public ConstraintsView() { InitializeComponent(); Loaded += OnLoaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is ConstraintsViewModel vm && vm.ValidateCommand.CanExecute(null)) vm.ValidateCommand.Execute(null); }
}
