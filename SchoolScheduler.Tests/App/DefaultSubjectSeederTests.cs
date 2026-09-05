using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class DefaultSubjectSeederTests
{
    [Fact]
    public async Task EnsureCreated_AddsDefaultsAndKeepsUserSubjectsWithoutDuplicates()
    {
        using var factory = new Factory();
        await using (var db = factory.CreateDbContext())
        {
            db.Subjects.AddRange(
                new Subject { Name = "Математика", ShortName = "Моя математика" },
                new Subject { Name = "Робототехника", ShortName = "Роботы", Type = SubjectType.Elective });
            await db.SaveChangesAsync();
        }
        var seeder = new DefaultSubjectSeeder(factory);

        var firstAdded = await seeder.EnsureCreatedAsync();
        var secondAdded = await seeder.EnsureCreatedAsync();

        Assert.Equal(23, firstAdded);
        Assert.Equal(0, secondAdded);
        await using var check = factory.CreateDbContext();
        Assert.Equal(25, await check.Subjects.CountAsync());
        Assert.Equal("Моя математика", (await check.Subjects.SingleAsync(x => x.Name == "Математика")).ShortName);
        Assert.Equal(SubjectType.Elective, (await check.Subjects.SingleAsync(x => x.Name == "Робототехника")).Type);
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=default-subjects-{Guid.NewGuid():N};Mode=Memory;Cache=Shared").Options;
        private readonly AppDbContext _keeper;

        public Factory()
        {
            _keeper = new AppDbContext(_options);
            _keeper.Database.OpenConnection();
            _keeper.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
