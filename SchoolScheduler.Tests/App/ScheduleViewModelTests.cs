using SchoolScheduler.App.Services;
using SchoolScheduler.App.ViewModels;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Tests.App;

public sealed class ScheduleViewModelTests
{
    [Fact]
    public async Task GeneratedSchedule_CanBeViewedByClassTeacherAndRoom()
    {
        var demand = new LessonDemand(1, 1, new(10, 20, 30, null, 40), false, false, 5, "");
        var slot = new TimeSlot(1, 1, 1, 1, default, default, false);
        var generated = new GeneratedSchedule(new([demand], [slot], [], []),
            new([new ScheduledLesson(1, 0, 1)], ScheduleScore.Empty, true, []),
            [new Teacher { Id = 10, FullName = "Иванова" }],
            [new Subject { Id = 20, Name = "Математика", ShortName = "Матем" }],
            [new SchoolClass { Id = 30, Name = "7Б", IsActive = true }], [],
            [new Room { Id = 40, Name = "12", IsActive = true }], 5);
        var vm = new ScheduleViewModel(new StubService(generated), new StubDialogs());
        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Contains("Матем", vm.Rows.Single().Monday.Lessons.Single().Text);
        vm.SelectedViewOption = vm.ViewOptions.Single(x => x.Mode == ScheduleViewMode.Teacher);
        Assert.Contains("7Б", vm.Rows.Single().Monday.Lessons.Single().Text);
        vm.SelectedViewOption = vm.ViewOptions.Single(x => x.Mode == ScheduleViewMode.Room);
        Assert.Contains("Иванова", vm.Rows.Single().Monday.Lessons.Single().Text);
        Assert.Equal(100, vm.Quality);
    }

    [Fact]
    public async Task ManualEdit_MovesSwapsPinsRejectsConflictAndUndoes()
    {
        var demands = new[]
        {
            new LessonDemand(1, 1, new(10, 20, 30, null, 40), false, false, 5, ""),
            new LessonDemand(2, 1, new(11, 21, 30, null, 41), false, false, 5, "")
        };
        var slots = new[]
        {
            new TimeSlot(1, 1, 1, 1, default, default, false),
            new TimeSlot(2, 1, 1, 2, default, default, false),
            new TimeSlot(3, 1, 2, 1, default, default, false)
        };
        var generated = BuildSchedule(demands, slots,
            [new ScheduledLesson(1, 0, 1), new ScheduledLesson(2, 0, 2)],
            [new ResourceAvailabilityConstraint(ResourceKind.Teacher, 10, new HashSet<int> { 1, 2 })]);
        var dialogs = new StubDialogs();
        var vm = new ScheduleViewModel(new StubService(generated), dialogs);
        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.True(vm.MoveLesson(new(1, 0), 1, 2)); // swap
        Assert.Contains("Предмет #20", vm.Rows.Single(x => x.LessonNumber == 2).Monday.Lessons.Single().Text);
        vm.UndoCommand.Execute(null);
        Assert.Contains("Предмет #20", vm.Rows.Single(x => x.LessonNumber == 1).Monday.Lessons.Single().Text);

        var item = vm.Rows.Single(x => x.LessonNumber == 1).Monday.Lessons.Single();
        vm.TogglePinCommand.Execute(item);
        Assert.True(item.Key == new ScheduleLessonKey(1, 0));
        Assert.False(vm.MoveLesson(item.Key, 1, 2));
        vm.TogglePinCommand.Execute(item);
        Assert.False(vm.MoveLesson(item.Key, 2, 1)); // unavailable
        Assert.Contains("недоступен", dialogs.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private static GeneratedSchedule BuildSchedule(IReadOnlyList<LessonDemand> demands, IReadOnlyList<TimeSlot> slots,
        IReadOnlyList<ScheduledLesson> lessons, IReadOnlyList<HardConstraint> constraints) =>
        new(new(demands, slots, constraints, []), new(lessons, ScheduleScore.Empty, true, []),
            [new Teacher { Id = 10, FullName = "Иванова" }, new Teacher { Id = 11, FullName = "Петров" }], [],
            [new SchoolClass { Id = 30, Name = "7Б", IsActive = true }], [],
            [new Room { Id = 40, Name = "12", IsActive = true }, new Room { Id = 41, Name = "13", IsActive = true }], 5);

    private sealed class StubService(GeneratedSchedule value) : IScheduleGenerationService
    { public Task<GeneratedSchedule> GenerateAsync(CancellationToken cancellationToken = default) => Task.FromResult(value); }
    private sealed class StubDialogs : IDialogService
    {
        public string LastError { get; private set; } = "";
        public void ShowMessage(string title, string message) { }
        public void ShowError(string message) => LastError = message;
    }
}
