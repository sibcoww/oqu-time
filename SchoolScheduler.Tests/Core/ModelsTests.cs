using SchoolScheduler.Core.Models;
using Xunit;

namespace SchoolScheduler.Tests.Core;

public class ModelsTests
{
    [Fact]
    public void SchoolModel_ShouldInitializeWithDefaultValues()
    {
        var school = new School();
        Assert.Equal(5, school.DaysPerWeek);
        Assert.Equal("KZ", school.Region);
        Assert.False(school.UseRegionalNorms);
    }

    [Fact]
    public void SchoolClassModel_ShouldInitializeWithDefaultValues()
    {
        var sc = new SchoolClass();
        Assert.True(sc.IsActive);
    }
}