using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<SchoolGroup> SchoolGroups => Set<SchoolGroup>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAvailability> RoomAvailabilities => Set<RoomAvailability>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<LessonPeriod> LessonPeriods => Set<LessonPeriod>();
    public DbSet<TeachingLoad> TeachingLoads => Set<TeachingLoad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TeacherAvailability>()
            .HasIndex(x => new { x.TeacherId, x.DayOfWeek, x.LessonNumber })
            .IsUnique();
        modelBuilder.Entity<TeacherAvailability>()
            .HasOne(x => x.Teacher)
            .WithMany(x => x.Availability)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RoomAvailability>()
            .HasIndex(x => new { x.RoomId, x.DayOfWeek, x.LessonNumber })
            .IsUnique();
        modelBuilder.Entity<RoomAvailability>()
            .HasOne(x => x.Room)
            .WithMany(x => x.Availability)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SchoolGroup>()
            .HasIndex(x => new { x.ClassId, x.Name })
            .IsUnique();
        modelBuilder.Entity<SchoolGroup>()
            .HasOne(x => x.Class)
            .WithMany()
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SchoolGroup>()
            .HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<TeachingLoad>()
            .Property(x => x.HoursPerWeek)
            .HasPrecision(6, 2);
        modelBuilder.Entity<TeachingLoad>().HasOne(x => x.Teacher).WithMany()
            .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TeachingLoad>().HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TeachingLoad>().HasOne(x => x.Class).WithMany()
            .HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TeachingLoad>().HasOne(x => x.Group).WithMany()
            .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<TeachingLoad>().HasOne(x => x.Room).WithMany()
            .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.SetNull);
        // Дополнительные настройки для связей, если понадобятся, можно добавить здесь
    }
}
