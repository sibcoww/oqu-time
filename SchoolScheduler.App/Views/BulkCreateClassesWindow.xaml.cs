using System.Windows;
using SchoolScheduler.App.ViewModels;

namespace SchoolScheduler.App.Views;

public partial class BulkCreateClassesWindow : Window
{
    public BulkCreateClassesWindow()
    {
        InitializeComponent();

        this.Loaded += BulkCreateClassesWindow_Loaded;
    }

    private void BulkCreateClassesWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BulkCreateClassesViewModel vm)
        {
            if (vm.LoadShiftsCommand.CanExecute(null))
            {
                vm.LoadShiftsCommand.Execute(null);
            }
        }
    }
}