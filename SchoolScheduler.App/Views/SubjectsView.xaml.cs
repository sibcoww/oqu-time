using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;
namespace SchoolScheduler.App.Views;
public partial class SubjectsView : UserControl
{
    public SubjectsView() { InitializeComponent(); Loaded += OnLoaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is SubjectsViewModel vm && vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null); }
}
