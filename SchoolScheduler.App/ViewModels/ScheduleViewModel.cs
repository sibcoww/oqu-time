using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.ImportExport;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.App.ViewModels;

public enum ScheduleViewMode { Class, Teacher, Room }
public sealed record ScheduleViewOption(ScheduleViewMode Mode, string Name);
public sealed record ScheduleResourceOption(int Id, string Name);
public sealed record SchedulePenaltyRow(string Name, int Value);
public sealed record ScheduleLessonKey(int DemandId, int OccurrenceIndex);
public partial class ScheduleLessonItem : ObservableObject
{
    public required ScheduleLessonKey Key { get; init; }
    public required string Text { get; init; }
    [ObservableProperty] private bool _isPinned;
}
public sealed record ScheduleCell(int Day, int LessonNumber, ObservableCollection<ScheduleLessonItem> Lessons);
public sealed record ScheduleGridRow(int LessonNumber, ScheduleCell Monday, ScheduleCell Tuesday, ScheduleCell Wednesday,
    ScheduleCell Thursday, ScheduleCell Friday, ScheduleCell Saturday);

public partial class ScheduleViewModel(IScheduleGenerationService service, IDialogService dialogs,
    ScheduleExcelService? excel = null, IFileDialogService? files = null,
    SchedulePdfService? pdf = null, SchedulePrintService? printer = null) : ViewModelBase
{
    private GeneratedSchedule? _schedule;
    private readonly HashSet<ScheduleLessonKey> _pinned = [];
    private readonly HashSet<ScheduleLessonKey> _manuallyEdited = [];
    private EditSnapshot? _undo;
    public IReadOnlyList<ScheduleViewOption> ViewOptions { get; } =
        [new(ScheduleViewMode.Class, "По классу"), new(ScheduleViewMode.Teacher, "По учителю"), new(ScheduleViewMode.Room, "По кабинету")];
    [ObservableProperty] private ScheduleViewOption? _selectedViewOption;
    [ObservableProperty] private ObservableCollection<ScheduleResourceOption> _resources = new();
    [ObservableProperty] private ScheduleResourceOption? _selectedResource;
    [ObservableProperty] private ObservableCollection<ScheduleGridRow> _rows = new();
    [ObservableProperty] private ObservableCollection<SchedulePenaltyRow> _penalties = new();
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _allowOptimizerToChangeManual;
    [ObservableProperty] private string _status = "Нажмите «Составить расписание».";
    [ObservableProperty] private int _quality;
    public IReadOnlyList<SchedulePageOrientation> PageOrientations { get; } = Enum.GetValues<SchedulePageOrientation>();
    public IReadOnlyList<SchedulePaperSize> PaperSizes { get; } = Enum.GetValues<SchedulePaperSize>();
    [ObservableProperty] private SchedulePageOrientation _selectedPageOrientation = SchedulePageOrientation.Landscape;
    [ObservableProperty] private SchedulePaperSize _selectedPaperSize = SchedulePaperSize.A4;
    [ObservableProperty] private bool _showTeachersInPrint = true;
    [ObservableProperty] private bool _showRoomsInPrint = true;
    [ObservableProperty] private ObservableCollection<string> _printClasses = new(["Все классы"]);
    [ObservableProperty] private string _selectedPrintClass = "Все классы";
    [ObservableProperty] private ObservableCollection<string> _printShifts = new(["Все смены"]);
    [ObservableProperty] private string _selectedPrintShift = "Все смены";

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsGenerating) return;
        try
        {
            IsGenerating = true; Status = "Составление расписания…"; Rows.Clear(); Penalties.Clear();
            _schedule = await service.GenerateAsync(); _pinned.Clear(); _manuallyEdited.Clear(); _undo = null; CanUndo = false;
            if (!_schedule.Candidate.IsFeasible)
            {
                Status = "Расписание не удалось составить.";
                var details = _schedule.Candidate.Infeasibility?.Reasons.Select(x => $"• {x.Message}\n  {x.Suggestion}") ?? _schedule.Candidate.Diagnostics;
                dialogs.ShowError("Расписание не удалось составить.\n\n" + string.Join("\n", details)); return;
            }
            Quality = Math.Max(0, 100 - Math.Min(100, _schedule.Candidate.Score.TotalPenalty));
            Penalties = new(_schedule.Candidate.Score.Penalties.OrderBy(x => x.Key).Select(x => new SchedulePenaltyRow(x.Key, x.Value)));
            Status = $"Расписание составлено. Качество: {Quality}/100. Жёстких нарушений: 0.";
            PrintClasses = new(["Все классы", .. _schedule.Classes.Where(x => x.IsActive).OrderBy(x => x.Parallel).ThenBy(x => x.Letter).Select(x => x.Name)]);
            PrintShifts = new(["Все смены", .. (_schedule.Shifts ?? []).Select(x => x.Name)]);
            SelectedPrintClass = PrintClasses[0]; SelectedPrintShift = PrintShifts[0];
            SelectedViewOption ??= ViewOptions[0]; RefreshResources();
        }
        catch (Exception ex) { Status = "Ошибка генерации."; dialogs.ShowError($"Не удалось составить расписание: {ex.Message}"); }
        finally { IsGenerating = false; }
    }

    public bool MoveLesson(ScheduleLessonKey key, int targetDay, int targetLesson)
    {
        if (_schedule is null) return false;
        if (_pinned.Contains(key)) return Reject("Закреплённое занятие нельзя переносить. Сначала снимите закрепление.");
        var source = _schedule.Candidate.Lessons.FirstOrDefault(x => x.LessonDemandId == key.DemandId && x.OccurrenceIndex == key.OccurrenceIndex);
        var sourceSlot = source is null ? null : _schedule.Problem.TimeSlots.First(x => x.Id == source.TimeSlotId);
        var targetSlot = _schedule.Problem.TimeSlots.FirstOrDefault(x => x.DayOfWeek == targetDay && x.LessonNumber == targetLesson && x.ShiftId == sourceSlot?.ShiftId);
        if (targetSlot is null || source is null || source.TimeSlotId == targetSlot.Id) return false;
        var target = _schedule.Candidate.Lessons.FirstOrDefault(x => x.TimeSlotId == targetSlot.Id && MatchesSelectedResource(x.LessonDemandId));
        var targetKey = target is null ? null : new ScheduleLessonKey(target.LessonDemandId, target.OccurrenceIndex);
        if (targetKey is not null && _pinned.Contains(targetKey)) return Reject("Нельзя обменять занятие с закреплённым.");
        var changed = _schedule.Candidate.Lessons.Select(x =>
            x.LessonDemandId == key.DemandId && x.OccurrenceIndex == key.OccurrenceIndex ? x with { TimeSlotId = targetSlot.Id } :
            target is not null && x.LessonDemandId == target.LessonDemandId && x.OccurrenceIndex == target.OccurrenceIndex ? x with { TimeSlotId = source.TimeSlotId } : x).ToList();
        var conflict = FindConflict(changed);
        if (conflict is not null) return Reject(conflict);
        SaveUndo();
        _schedule = _schedule with { Candidate = _schedule.Candidate with { Lessons = changed } };
        _manuallyEdited.Add(key);
        if (targetKey is not null) _manuallyEdited.Add(targetKey);
        CanUndo = true; Status = target is null ? "Занятие перенесено. Конфликтов нет." : "Занятия обменены. Конфликтов нет.";
        RebuildGrid(); return true;
    }

    [RelayCommand]
    private async Task ReoptimizeAsync()
    {
        if (_schedule is null || IsGenerating) return;
        try
        {
            IsGenerating = true; Status = "Повторная оптимизация…";
            var preservedKeys = AllowOptimizerToChangeManual ? _pinned : _pinned.Concat(_manuallyEdited).ToHashSet();
            var preserved = _schedule.Candidate.Lessons
                .Where(x => preservedKeys.Contains(new(x.LessonDemandId, x.OccurrenceIndex)))
                .Select(x => new PreservedScheduleAssignment(x.LessonDemandId, x.OccurrenceIndex, x.TimeSlotId)).ToList();
            var result = await service.ReoptimizeAsync(_schedule, preserved);
            if (!result.Candidate.IsFeasible) { Reject("Не удалось повторно оптимизировать расписание с выбранными закреплениями."); return; }
            SaveUndo(); _schedule = result;
            if (AllowOptimizerToChangeManual) _manuallyEdited.Clear();
            UpdateScore();
            Status = $"Расписание повторно оптимизировано. Сохранено решений: {preserved.Count}. Качество: {Quality}/100.";
            RebuildGrid();
        }
        catch (Exception ex) { dialogs.ShowError($"Не удалось повторно оптимизировать расписание: {ex.Message}"); Status = "Ошибка повторной оптимизации."; }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private void TogglePin(ScheduleLessonItem? item)
    {
        if (item is null) return;
        SaveUndo();
        if (!_pinned.Add(item.Key)) _pinned.Remove(item.Key);
        Status = _pinned.Contains(item.Key) ? "Занятие закреплено." : "Закрепление снято."; RebuildGrid();
    }

    [RelayCommand]
    private void Export()
    {
        if (_schedule is null || !_schedule.Candidate.IsFeasible || excel is null || files is null) return;
        var path = files.ChooseExcelSavePath($"Расписание-{DateTime.Today:yyyy-MM-dd}.xlsx");
        if (path is null) return;
        try
        {
            excel.Export(path, BuildExportRows());
            Status = $"Расписание экспортировано: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = "Ошибка экспорта расписания.";
            dialogs.ShowError($"Не удалось экспортировать расписание: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (_schedule is null || pdf is null || files is null) return;
        var path = files.ChoosePdfSavePath($"Расписание-{DateTime.Today:yyyy-MM-dd}.pdf");
        if (path is null) return;
        try
        {
            pdf.Export(path, BuildPrintData(), BuildPrintSettings());
            Status = $"PDF создан: {Path.GetFileName(path)}";
        }
        catch (Exception ex) { Status = "Ошибка экспорта PDF."; dialogs.ShowError($"Не удалось создать PDF: {ex.Message}"); }
    }

    [RelayCommand]
    private void Print()
    {
        if (_schedule is null || printer is null) return;
        try
        {
            if (printer.Print(BuildPrintData(), BuildPrintSettings())) Status = "Расписание отправлено на печать.";
        }
        catch (Exception ex) { Status = "Ошибка печати."; dialogs.ShowError($"Не удалось напечатать расписание: {ex.Message}"); }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_schedule is null || _undo is null) return;
        _schedule = _schedule with { Candidate = _schedule.Candidate with { Lessons = _undo.Lessons } };
        _pinned.Clear(); _pinned.UnionWith(_undo.Pinned);
        _manuallyEdited.Clear(); _manuallyEdited.UnionWith(_undo.ManuallyEdited);
        _undo = null; CanUndo = false; Status = "Последняя операция отменена."; RebuildGrid();
    }

    partial void OnSelectedViewOptionChanged(ScheduleViewOption? value) => RefreshResources();
    partial void OnSelectedResourceChanged(ScheduleResourceOption? value) => RebuildGrid();
    private bool Reject(string message) { Status = $"Конфликт: {message}"; dialogs.ShowError(message); return false; }
    private void SaveUndo()
    {
        if (_schedule is null) return;
        _undo = new(_schedule.Candidate.Lessons.ToList(), _pinned.ToHashSet(), _manuallyEdited.ToHashSet()); CanUndo = true;
    }

    private string? FindConflict(IReadOnlyList<ScheduledLesson> lessons)
    {
        var demands = _schedule!.Problem.Demands.ToDictionary(x => x.Id);
        foreach (var fixedAssignment in _schedule.Problem.HardConstraints.OfType<FixedAssignmentConstraint>())
            if (lessons.Any(x => x.LessonDemandId == fixedAssignment.LessonDemandId && x.TimeSlotId != fixedAssignment.TimeSlotId))
                return "Занятие имеет фиксированное время и не может быть перенесено.";
        foreach (var lesson in lessons)
            foreach (var availability in _schedule.Problem.HardConstraints.OfType<ResourceAvailabilityConstraint>())
            {
                var r = demands[lesson.LessonDemandId].Resources;
                var applies = availability.ResourceKind switch { ResourceKind.Teacher => r.TeacherId == availability.ResourceId,
                    ResourceKind.Class => r.ClassId == availability.ResourceId, ResourceKind.Group => r.GroupId == availability.ResourceId,
                    ResourceKind.Room => r.RoomId == availability.ResourceId, _ => false };
                if (applies && !availability.AllowedTimeSlotIds.Contains(lesson.TimeSlotId)) return "Ресурс недоступен в выбранное время.";
            }
        foreach (var group in lessons.GroupBy(x => x.TimeSlotId))
        {
            var r = group.Select(x => demands[x.LessonDemandId].Resources).ToList();
            if (HasDuplicate(r.Select(x => x.TeacherId)) || HasDuplicate(r.Select(x => x.ClassId)) ||
                HasDuplicate(r.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value)) ||
                HasDuplicate(r.Where(x => x.RoomId.HasValue).Select(x => x.RoomId!.Value)))
                return "Учитель, класс, группа или кабинет уже заняты в выбранное время.";
        }
        return null;
    }
    private static bool HasDuplicate(IEnumerable<int> values) => values.GroupBy(x => x).Any(x => x.Count() > 1);

    private void RefreshResources()
    {
        if (_schedule is null || SelectedViewOption is null) return;
        Resources = SelectedViewOption.Mode switch
        {
            ScheduleViewMode.Class => new(_schedule.Classes.Where(x => x.IsActive).OrderBy(x => x.Parallel).ThenBy(x => x.Letter).Select(x => new ScheduleResourceOption(x.Id, x.Name))),
            ScheduleViewMode.Teacher => new(_schedule.Teachers.Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new ScheduleResourceOption(x.Id, x.FullName))),
            _ => new(_schedule.Rooms.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new ScheduleResourceOption(x.Id, x.Name)))
        };
        SelectedResource = Resources.FirstOrDefault(); RebuildGrid();
    }
    private void RebuildGrid()
    {
        if (_schedule is null || SelectedViewOption is null || SelectedResource is null) { Rows.Clear(); return; }
        Rows = new(_schedule.Problem.TimeSlots.Select(x => x.LessonNumber).Distinct().Order().Select(n =>
            new ScheduleGridRow(n, Cell(1, n), Cell(2, n), Cell(3, n), Cell(4, n), Cell(5, n), Cell(6, n))));
    }
    private ScheduleCell Cell(int day, int lessonNumber)
    {
        var items = new ObservableCollection<ScheduleLessonItem>();
        if (_schedule is not null && day <= _schedule.DaysPerWeek)
        {
            var demands = _schedule.Problem.Demands.ToDictionary(x => x.Id); var slots = _schedule.Problem.TimeSlots.ToDictionary(x => x.Id);
            foreach (var lesson in _schedule.Candidate.Lessons.Where(x => slots[x.TimeSlotId].DayOfWeek == day && slots[x.TimeSlotId].LessonNumber == lessonNumber && MatchesSelectedResource(x.LessonDemandId)))
            {
                var key = new ScheduleLessonKey(lesson.LessonDemandId, lesson.OccurrenceIndex);
                items.Add(new() { Key = key, Text = Describe(demands[lesson.LessonDemandId]), IsPinned = _pinned.Contains(key) });
            }
        }
        return new(day, lessonNumber, items);
    }
    private bool MatchesSelectedResource(int demandId)
    {
        var d = _schedule!.Problem.Demands.First(x => x.Id == demandId);
        return SelectedViewOption!.Mode switch { ScheduleViewMode.Class => d.Resources.ClassId == SelectedResource!.Id,
            ScheduleViewMode.Teacher => d.Resources.TeacherId == SelectedResource!.Id, _ => d.Resources.RoomId == SelectedResource!.Id };
    }
    private string Describe(LessonDemand d)
    {
        var subject = _schedule!.Subjects.FirstOrDefault(x => x.Id == d.Resources.SubjectId)?.ShortName ?? $"Предмет #{d.Resources.SubjectId}";
        var teacher = _schedule.Teachers.FirstOrDefault(x => x.Id == d.Resources.TeacherId)?.FullName ?? $"Учитель #{d.Resources.TeacherId}";
        var schoolClass = _schedule.Classes.FirstOrDefault(x => x.Id == d.Resources.ClassId)?.Name ?? $"Класс #{d.Resources.ClassId}";
        var group = d.Resources.GroupId.HasValue ? _schedule.Groups.FirstOrDefault(x => x.Id == d.Resources.GroupId)?.Name : null;
        var room = d.Resources.RoomId.HasValue ? _schedule.Rooms.FirstOrDefault(x => x.Id == d.Resources.RoomId)?.Name : null;
        return SelectedViewOption!.Mode switch { ScheduleViewMode.Class => Join(subject, group, teacher, room),
            ScheduleViewMode.Teacher => Join(schoolClass, group, subject, room), _ => Join(schoolClass, group, subject, teacher) };
    }
    private static string Join(params string?[] values) => string.Join(" · ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
    private void UpdateScore()
    {
        Quality = Math.Max(0, 100 - Math.Min(100, _schedule!.Candidate.Score.TotalPenalty));
        Penalties = new(_schedule.Candidate.Score.Penalties.OrderBy(x => x.Key).Select(x => new SchedulePenaltyRow(x.Key, x.Value)));
    }
    private IReadOnlyList<ScheduleExportRow> BuildExportRows()
    {
        var demands = _schedule!.Problem.Demands.ToDictionary(x => x.Id);
        var slots = _schedule.Problem.TimeSlots.ToDictionary(x => x.Id);
        var shifts = (_schedule.Shifts ?? []).ToDictionary(x => x.Id, x => x.Name);
        return _schedule.Candidate.Lessons.Select(lesson =>
        {
            var demand = demands[lesson.LessonDemandId]; var slot = slots[lesson.TimeSlotId];
            return new ScheduleExportRow(slot.DayOfWeek, slot.LessonNumber,
                _schedule.Classes.FirstOrDefault(x => x.Id == demand.Resources.ClassId)?.Name ?? $"Класс #{demand.Resources.ClassId}",
                demand.Resources.GroupId is int groupId ? _schedule.Groups.FirstOrDefault(x => x.Id == groupId)?.Name : null,
                _schedule.Subjects.FirstOrDefault(x => x.Id == demand.Resources.SubjectId)?.Name ?? $"Предмет #{demand.Resources.SubjectId}",
                _schedule.Teachers.FirstOrDefault(x => x.Id == demand.Resources.TeacherId)?.FullName ?? $"Учитель #{demand.Resources.TeacherId}",
                demand.Resources.RoomId is int roomId ? _schedule.Rooms.FirstOrDefault(x => x.Id == roomId)?.Name : null,
                shifts.GetValueOrDefault(slot.ShiftId, $"Смена #{slot.ShiftId}"));
        }).ToList();
    }
    private SchedulePrintData BuildPrintData()
    {
        var rows = BuildExportRows().Where(x => SelectedPrintShift == "Все смены" || x.Shift == SelectedPrintShift).ToList();
        return new(_schedule!.SchoolName, _schedule.AcademicYearName, DateTimeOffset.Now, rows);
    }
    private SchedulePrintSettings BuildPrintSettings() => new(SelectedPaperSize, SelectedPageOrientation,
        ShowTeachersInPrint, ShowRoomsInPrint,
        SelectedPrintClass == "Все классы" ? null : new HashSet<string> { SelectedPrintClass },
        SelectedPrintShift == "Все смены" ? null : SelectedPrintShift);
    private sealed record EditSnapshot(IReadOnlyList<ScheduledLesson> Lessons, IReadOnlySet<ScheduleLessonKey> Pinned,
        IReadOnlySet<ScheduleLessonKey> ManuallyEdited);
}
