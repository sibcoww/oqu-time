using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Data;
using SchoolScheduler.Scheduling.Normative;

namespace SchoolScheduler.App.Services;

public sealed class NormativeValidationService(IDbContextFactory<AppDbContext> factory,
    INormativeRuleSet ruleSet) : INormativeValidationService
{
    public async Task<NormativeCheckResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var context = new NormativeContext(
            await db.SchoolClasses.AsNoTracking().ToListAsync(cancellationToken),
            await db.TeachingLoads.AsNoTracking().ToListAsync(cancellationToken),
            await db.Shifts.AsNoTracking().ToListAsync(cancellationToken),
            await db.LessonPeriods.AsNoTracking().ToListAsync(cancellationToken));
        return new(ruleSet, ruleSet.Evaluate(context));
    }
}
