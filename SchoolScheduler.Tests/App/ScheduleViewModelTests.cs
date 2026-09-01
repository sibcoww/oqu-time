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

        Assert.Contains("Матем", vm.Rows.Single().Monday);
        vm.SelectedViewOption = vm.ViewOptions.Single(x => x.Mode == ScheduleViewMode.Teacher);
        Assert.Contains("7Б", vm.Rows.Single().Monday);
        vm.SelectedViewOption = vm.ViewOptions.Single(x => x.Mode == ScheduleViewMode.Room);
        Assert.Contains("Иванова", vm.Rows.Single().Monday);
        Assert.Equal(100, vm.Quality);
    }

    private sealed class StubService(GeneratedSchedule value) : IScheduleGenerationService
    { public Task<GeneratedSchedule> GenerateAsync(CancellationToken cancellationToken = default) => Task.FromResult(value); }
    private sealed class StubDialogs : IDialogService
    { public void ShowMessage(string title, string message) { } public void ShowError(string message) { } }
}
