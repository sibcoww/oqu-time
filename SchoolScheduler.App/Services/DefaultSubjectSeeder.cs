using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public sealed class DefaultSubjectSeeder(IDbContextFactory<AppDbContext> factory)
{
    public async Task<int> EnsureCreatedAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var existingNames = (await db.Subjects.AsNoTracking().Select(x => x.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = DefaultSubjects.Where(x => !existingNames.Contains(x.Name))
            .Select(x => new Subject
            {
                Name = x.Name,
                ShortName = x.ShortName,
                Difficulty = x.Difficulty,
                Type = SubjectType.Required
            }).ToList();
        if (missing.Count == 0) return 0;
        db.Subjects.AddRange(missing);
        await db.SaveChangesAsync();
        return missing.Count;
    }

    private static readonly (string Name, string ShortName, int Difficulty)[] DefaultSubjects =
    [
        ("Математика", "Матем.", 7),
        ("Алгебра", "Алгебра", 8),
        ("Геометрия", "Геометр.", 8),
        ("Русский язык", "Рус. яз.", 6),
        ("Русская литература", "Рус. лит.", 5),
        ("Казахский язык", "Каз. яз.", 6),
        ("Казахская литература", "Каз. лит.", 5),
        ("Английский язык", "Англ. яз.", 6),
        ("История Казахстана", "Ист. Каз.", 6),
        ("Всемирная история", "Всем. ист.", 6),
        ("География", "Географ.", 5),
        ("Биология", "Биология", 6),
        ("Физика", "Физика", 8),
        ("Химия", "Химия", 8),
        ("Информатика", "Информ.", 6),
        ("Естествознание", "Естеств.", 5),
        ("Познание мира", "Позн. мира", 4),
        ("Цифровая грамотность", "Цифр. грам.", 4),
        ("Физическая культура", "Физ-ра", 3),
        ("Музыка", "Музыка", 3),
        ("Изобразительное искусство", "ИЗО", 3),
        ("Художественный труд", "Худ. труд", 3),
        ("Основы права", "Осн. права", 5),
        ("Начальная военная и технологическая подготовка", "НВТП", 5)
    ];
}
