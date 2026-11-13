using JobRadar.Core.Models;

namespace JobRadar.Core.Tests;

/// <summary>Every enum value of SPEC §6.4 exists, with the documented numbers for SourceKind.</summary>
public class EnumContractTests
{
    private static string[] Names<T>() where T : struct, Enum => Enum.GetNames<T>();

    [Fact]
    public void Source_kind_precedence_numbers()
    {
        Assert.Equal(3, (int)SourceKind.Ats);
        Assert.Equal(2, (int)SourceKind.Portal);
        Assert.Equal(1, (int)SourceKind.Aggregator);
    }

    [Fact]
    public void Lifecycle_liveness_workmode_track_have_the_spec_values()
    {
        Assert.Equal(["New", "Active", "Expired", "Archived"], Names<Lifecycle>());
        Assert.Equal(["Unknown", "Alive", "Dead", "Unverified"], Names<Liveness>());
        Assert.Equal(["Remote", "Hybrid", "Onsite", "Unknown"], Names<WorkMode>());
        Assert.Equal(["A", "B", "C", "None"], Names<Track>());
        Assert.Equal(["Year", "Month", "Day", "Hour"], Names<SalaryPeriod>());
    }

    [Fact]
    public void Extraction_enums_have_the_spec_values()
    {
        Assert.Equal(["AiEngineer", "Backend", "Fullstack", "Data", "Devops", "Other"], Names<RoleFamily>());
        Assert.Equal(["Dotnet", "Python", "Java", "Typescript", "Go", "Other", "Unknown"], Names<StackMain>());
        Assert.Equal(["Junior", "Mid", "Senior", "Lead", "Unspecified"], Names<Seniority>());
        Assert.Equal(["Yes", "No", "Unknown"], Names<B2bAllowed>());
        Assert.Equal(["Direct", "Agency", "Consulting", "Unknown"], Names<EmployerType>());
        Assert.Equal(["Low", "Medium", "High"], Names<Confidence>());
    }

    [Fact]
    public void Enum_json_names_are_snake_case()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(RoleFamily.AiEngineer);
        Assert.Equal("\"ai_engineer\"", json);
    }
}
