using System.Windows;
using System.Windows.Controls;
using SchoolScheduler.App.ViewModels;

namespace SchoolScheduler.App.Views;

public partial class ClassesView : UserControl
{
    public ClassesView()
    {
        InitializeComponent();

        this.Loaded += ClassesView_Loaded;
    }

    private void ClassesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClassesViewModel vm)
        {
            if (vm.LoadDataCommand.CanExecute(null))
            {
                vm.LoadDataCommand.Execute(null);
            }
        }
    }
}