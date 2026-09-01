using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class SchoolClassServiceTests
{
    [Fact]
    public async Task ClassExists_IsCaseInsensitive_AndCanExcludeEditedClass()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var db = factory.CreateDbContext())
        {
            db.SchoolClasses.Add(new SchoolClass { Name = "5А", Parallel = 5, Letter = "А", ShiftId = 1, MaxLessonsPerDay = 6 });
            await db.SaveChangesAsync();
        }

        var service = new SchoolClassService(factory);
        var saved = (await service.GetAllClassesAsync()).Single();

        Assert.True(await service.ClassExistsAsync(5, " а "));
        Assert.False(await service.ClassExistsAsync(5, "а", saved.Id));
    }

    [Fact]
    public async Task BulkCreate_SkipsExistingClasses()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var db = factory.CreateDbContext())
        {
            db.SchoolClasses.Add(new SchoolClass { Name = "1А", Parallel = 1, Letter = "А", ShiftId = 1, MaxLessonsPerDay = 6 });
            await db.SaveChangesAsync();
        }

        var service = new SchoolClassService(factory);
        await service.BulkCreateClassesAsync(1, 2, ["А", "Б"], 1, 6);

        var classes = await service.GetAllClassesAsync();
        Assert.Equal(4, classes.Count);
        Assert.Equal(4, classes.Select(x => (x.Parallel, x.Letter)).Distinct().Count());
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=school-class-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared")
            .Options;

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        private readonly AppDbContext _connectionKeeper = CreateKeeper(options);

        public AppDbContext CreateDbContext() => new(options);

        private static AppDbContext CreateKeeper(DbContextOptions<AppDbContext> options)
        {
            var context = new AppDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }
    }
}
