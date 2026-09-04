using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class CatalogServiceTests
{
    [Fact]
    public async Task Subject_RoundTripsAllSchedulingProperties()
    {
        using var factory = new Factory(Options());
        var service = new CatalogService(factory);
        var saved = await service.SaveSubjectAsync(new Subject
        { Name = "Информатика", ShortName = "Инф", Difficulty = 7, Type = SubjectType.Required, AllowDoubleLessons = true });

        var subject = Assert.Single(await service.GetSubjectsAsync());
        Assert.Equal(saved.Id, subject.Id);
        Assert.Equal("Инф", subject.ShortName);
        Assert.Equal(7, subject.Difficulty);
        Assert.True(subject.AllowDoubleLessons);
        Assert.True(await service.SubjectExistsAsync(" информатика "));
    }

    [Fact]
    public async Task Room_RoundTripsAvailability_AndCanBeArchived()
    {
        using var factory = new Factory(Options());
        var service = new CatalogService(factory);
        var room = await service.SaveRoomAsync(new Room { Name = "Спортзал", Type = RoomType.Gym },
            [new RoomAvailability { DayOfWeek = 2, LessonPeriodId = 3, IsAvailable = false }]);

        var details = await service.GetRoomAsync(room.Id);
        Assert.Equal(RoomType.Gym, details!.Type);
        Assert.False(details.Availability.Single().IsAvailable);
        Assert.True(await service.RoomExistsAsync("спортзал"));

        await service.ArchiveRoomAsync(room.Id);
        Assert.False((await service.GetRoomAsync(room.Id))!.IsActive);
    }

    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source=catalog-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared").Options;

    private sealed class Factory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _keeper;
        public Factory(DbContextOptions<AppDbContext> options)
        { _options = options; _keeper = new(options); _keeper.Database.OpenConnection(); _keeper.Database.EnsureCreated();
          _keeper.Shifts.Add(new Shift { Id = 1, Name = "Shift", LessonPeriods = [new LessonPeriod { Id = 3, Number = 3 }] }); _keeper.SaveChanges(); }
        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
