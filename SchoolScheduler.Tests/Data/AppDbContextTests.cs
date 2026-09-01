using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;
using Xunit;

namespace SchoolScheduler.Tests.Data;

public class AppDbContextTests
{
    [Fact]
    public void CanSaveAndReadSchool()
    {
        // Must keep connection open manually in memory Sqlite db
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new AppDbContext(options))
        {
            context.Database.EnsureCreated();

            var school = new School { Name = "Test School", DaysPerWeek = 6 };
            context.Schools.Add(school);
            context.SaveChanges();
        }

        using (var context = new AppDbContext(options))
        {
            var savedSchool = Assert.Single(context.Schools);
            Assert.Equal("Test School", savedSchool.Name);
            Assert.Equal(6, savedSchool.DaysPerWeek);
        }
    }
}