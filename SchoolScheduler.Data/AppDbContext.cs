using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<LessonPeriod> LessonPeriods => Set<LessonPeriod>();
    public DbSet<TeachingLoad> TeachingLoads => Set<TeachingLoad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Дополнительные настройки для связей, если понадобятся, можно добавить здесь
    }
}