using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.App.ViewModels;

public enum ScheduleViewMode { Class, Teacher, Room }
public sealed record ScheduleViewOption(ScheduleViewMode Mode, string Name);
public sealed record ScheduleResourceOption(int Id, string Name);
public sealed record SchedulePenaltyRow(string Name, int Value);

public partial class ScheduleViewModel(IScheduleGenerationService service, IDialogService dialogs) : ViewModelBase
{
    private GeneratedSchedule? _schedule;
    public IReadOnlyList<ScheduleViewOption> ViewOptions { get; } =
    [new(ScheduleViewMode.Class, "По классу"), new(ScheduleViewMode.Teacher, "По учителю"), new(ScheduleViewMode.Room, "По кабинету")];
    [ObservableProperty] private ScheduleViewOption? _selectedViewOption;
    [ObservableProperty] private ObservableCollection<ScheduleResourceOption> _resources = new();
    [ObservableProperty] private ScheduleResourceOption? _selectedResource;
    [ObservableProperty] private ObservableCollection<ScheduleGridRow> _rows = new();
    [ObservableProperty] private ObservableCollection<SchedulePenaltyRow> _penalties = new();
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string _status = "Нажмите «Составить расписание».";
    [ObservableProperty] private int _quality;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsGenerating) return;
        try
        {
            IsGenerating = true; Status = "Составление расписания…"; Rows.Clear(); Penalties.Clear();
            _schedule = await service.GenerateAsync();
            if (!_schedule.Candidate.IsFeasible)
            {
                Status = "Расписание не удалось составить.";
                var details = _schedule.Candidate.Infeasibility?.Reasons.Select(x => $"• {x.Message}\n  {x.Suggestion}")
                    ?? _schedule.Candidate.Diagnostics;
                dialogs.ShowError("Расписание не удалось составить.\n\n" + string.Join("\n", details));
                return;
            }
            Quality = Math.Max(0, 100 - Math.Min(100, _schedule.Candidate.Score.TotalPenalty));
            Penalties = new(_schedule.Candidate.Score.Penalties.OrderBy(x => x.Key).Select(x => new SchedulePenaltyRow(x.Key, x.Value)));
            Status = $"Расписание составлено. Качество: {Quality}/100. Жёстких нарушений: 0.";
            SelectedViewOption ??= ViewOptions[0]; RefreshResources();
        }
        catch (Exception ex) { Status = "Ошибка генерации."; dialogs.ShowError($"Не удалось составить расписание: {ex.Message}"); }
        finally { IsGenerating = false; }
    }

    partial void OnSelectedViewOptionChanged(ScheduleViewOption? value) => RefreshResources();
    partial void OnSelectedResourceChanged(ScheduleResourceOption? value) => RebuildGrid();

    private void RefreshResources()
    {
        if (_schedule is null || SelectedViewOption is null) return;
        Resources = SelectedViewOption.Mode switch
        {
            ScheduleViewMode.Class => new(_schedule.Classes.Where(x => x.IsActive).OrderBy(x => x.Parallel).ThenBy(x => x.Letter).Select(x => new ScheduleResourceOption(x.Id, x.Name))),
            ScheduleViewMode.Teacher => new(_schedule.Teachers.Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new ScheduleResourceOption(x.Id, x.FullName))),
            _ => new(_schedule.Rooms.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new ScheduleResourceOption(x.Id, x.Name)))
        };
        SelectedResource = Resources.FirstOrDefault();
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (_schedule is null || SelectedViewOption is null || SelectedResource is null) { Rows.Clear(); return; }
        var lessonNumbers = _schedule.Problem.TimeSlots.Select(x => x.LessonNumber).Distinct().Order().ToList();
        Rows = new(lessonNumbers.Select(lesson => new ScheduleGridRow(lesson,
            Cell(1, lesson), Cell(2, lesson), Cell(3, lesson), Cell(4, lesson), Cell(5, lesson), Cell(6, lesson))));
    }

    private string Cell(int day, int lessonNumber)
    {
        if (_schedule is null || SelectedViewOption is null || SelectedResource is null || day > _schedule.DaysPerWeek) return string.Empty;
        var demands = _schedule.Problem.Demands.ToDictionary(x => x.Id);
        var slots = _schedule.Problem.TimeSlots.ToDictionary(x => x.Id);
        var matches = _schedule.Candidate.Lessons.Where(x => slots[x.TimeSlotId].DayOfWeek == day && slots[x.TimeSlotId].LessonNumber == lessonNumber)
            .Select(x => demands[x.LessonDemandId]).Where(MatchesResource).ToList();
        return string.Join(Environment.NewLine, matches.Select(Describe));
    }

    private bool MatchesResource(LessonDemand demand) => SelectedViewOption!.Mode switch
    {
        ScheduleViewMode.Class => demand.Resources.ClassId == SelectedResource!.Id,
        ScheduleViewMode.Teacher => demand.Resources.TeacherId == SelectedResource!.Id,
        _ => demand.Resources.RoomId == SelectedResource!.Id
    };

    private string Describe(LessonDemand demand)
    {
        var subject = _schedule!.Subjects.FirstOrDefault(x => x.Id == demand.Resources.SubjectId)?.ShortName ?? $"Предмет #{demand.Resources.SubjectId}";
        var teacher = _schedule.Teachers.FirstOrDefault(x => x.Id == demand.Resources.TeacherId)?.FullName ?? $"Учитель #{demand.Resources.TeacherId}";
        var schoolClass = _schedule.Classes.FirstOrDefault(x => x.Id == demand.Resources.ClassId)?.Name ?? $"Класс #{demand.Resources.ClassId}";
        var group = demand.Resources.GroupId.HasValue ? _schedule.Groups.FirstOrDefault(x => x.Id == demand.Resources.GroupId)?.Name : null;
        var room = demand.Resources.RoomId.HasValue ? _schedule.Rooms.FirstOrDefault(x => x.Id == demand.Resources.RoomId)?.Name : null;
        return SelectedViewOption!.Mode switch
        {
            ScheduleViewMode.Class => string.Join(" · ", new[] { subject, group, teacher, room }.Where(x => !string.IsNullOrWhiteSpace(x))),
            ScheduleViewMode.Teacher => string.Join(" · ", new[] { schoolClass, group, subject, room }.Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => string.Join(" · ", new[] { schoolClass, group, subject, teacher }.Where(x => !string.IsNullOrWhiteSpace(x)))
        };
    }
}

public sealed record ScheduleGridRow(int LessonNumber, string Monday, string Tuesday, string Wednesday,
    string Thursday, string Friday, string Saturday);
