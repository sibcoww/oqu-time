using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchoolScheduler.App.ViewModels;

namespace SchoolScheduler.App.Views;
public partial class ScheduleView : UserControl
{
    private Point _dragStart;
    public ScheduleView() => InitializeComponent();
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) { _dragStart = e.GetPosition(this); base.OnPreviewMouseLeftButtonDown(e); }
    private void Lesson_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: ScheduleLessonItem item }) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(ScheduleLessonKey), item.Key), DragDropEffects.Move);
    }
    private void Cell_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(typeof(ScheduleLessonKey)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }
    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ScheduleCell cell } && e.Data.GetData(typeof(ScheduleLessonKey)) is ScheduleLessonKey key && DataContext is ScheduleViewModel vm) vm.MoveLesson(key, cell.Day, cell.LessonNumber);
        e.Handled = true;
    }
    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { Tag: ScheduleLessonItem item } } } && DataContext is ScheduleViewModel vm) vm.TogglePinCommand.Execute(item);
    }
}
