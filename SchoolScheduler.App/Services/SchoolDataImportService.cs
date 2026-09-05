using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.App.Services;

public sealed record SchoolDataImportSummary(int Teachers, int Subjects, int Classes, int Rooms,
    int Groups, int LoadsAdded, int LoadsUpdated);

public sealed partial class SchoolDataImportService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<SchoolDataImportSummary> ImportAsync(IReadOnlyCollection<SchoolDataImportRow> rows)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var shifts = await db.Shifts.OrderBy(x => x.Id).Include(x => x.LessonPeriods).ToListAsync();
        var shift = shifts.FirstOrDefault() ?? throw new InvalidOperationException("Сначала настройте хотя бы одну смену.");
        var maxLessons = Math.Max(1, shift.LessonPeriods.Count);

        var teachers = (await db.Teachers.ToListAsync()).ToDictionary(x => x.FullName, StringComparer.OrdinalIgnoreCase);
        var subjects = (await db.Subjects.ToListAsync()).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var classes = (await db.SchoolClasses.ToListAsync()).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var rooms = (await db.Rooms.ToListAsync()).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var teacherCount = 0; var subjectCount = 0; var classCount = 0; var roomCount = 0;

        foreach (var row in rows)
        {
            if (!teachers.ContainsKey(row.Teacher)) { var value = new Teacher { FullName = row.Teacher }; teachers.Add(row.Teacher, value); db.Add(value); teacherCount++; }
            if (!subjects.ContainsKey(row.Subject)) { var value = new Subject { Name = row.Subject, ShortName = row.ShortSubject, Difficulty = 5 }; subjects.Add(row.Subject, value); db.Add(value); subjectCount++; }
            if (!classes.ContainsKey(row.SchoolClass))
            {
                var match = ClassName().Match(row.SchoolClass);
                var value = new SchoolClass { Name = row.SchoolClass, Parallel = match.Success ? int.Parse(match.Groups[1].Value) : 1,
                    Letter = match.Success ? match.Groups[2].Value.ToUpperInvariant() : row.SchoolClass, ShiftId = shift.Id, MaxLessonsPerDay = maxLessons };
                classes.Add(row.SchoolClass, value); db.Add(value); classCount++;
            }
            if (row.Room is not null && !rooms.ContainsKey(row.Room)) { var value = new Room { Name = row.Room }; rooms.Add(row.Room, value); db.Add(value); roomCount++; }
        }
        await db.SaveChangesAsync();

        var groups = await db.SchoolGroups.ToListAsync(); var groupCount = 0;
        foreach (var row in rows.Where(x => x.Group is not null))
        {
            var schoolClass = classes[row.SchoolClass];
            if (groups.Any(x => x.ClassId == schoolClass.Id && x.Name.Equals(row.Group!, StringComparison.OrdinalIgnoreCase))) continue;
            var group = new SchoolGroup { Name = row.Group!, ClassId = schoolClass.Id, SubjectId = subjects[row.Subject].Id };
            groups.Add(group); db.Add(group); groupCount++;
        }
        await db.SaveChangesAsync();

        var loads = await db.TeachingLoads.ToListAsync(); var added = 0; var updated = 0;
        foreach (var row in rows)
        {
            var teacherId = teachers[row.Teacher].Id; var subjectId = subjects[row.Subject].Id; var classId = classes[row.SchoolClass].Id;
            int? roomId = row.Room is null ? null : rooms[row.Room].Id;
            int? groupId = row.Group is null ? null : groups.First(x => x.ClassId == classId && x.Name.Equals(row.Group, StringComparison.OrdinalIgnoreCase)).Id;
            var existing = loads.FirstOrDefault(x => x.TeacherId == teacherId && x.SubjectId == subjectId && x.ClassId == classId && x.GroupId == groupId);
            if (existing is null)
            {
                existing = new TeachingLoad { TeacherId = teacherId, SubjectId = subjectId, ClassId = classId, GroupId = groupId };
                loads.Add(existing); db.Add(existing); added++;
            }
            else updated++;
            existing.HoursPerWeek = row.HoursPerWeek; existing.RoomId = roomId; existing.AllowZeroLesson = row.AllowZeroLesson;
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return new(teacherCount, subjectCount, classCount, roomCount, groupCount, added, updated);
    }

    [GeneratedRegex(@"^(\d+)(.*)$")]
    private static partial Regex ClassName();
}
