using Microsoft.Extensions.Configuration;
using Wildlife_Sports_Day_Server.Infrastructure.Configuration;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Infrastructure.Configuration;

public class ProductionConfigurationValidatorTests
{
    public static TheoryData<string> RequiredConfigurationKeys =>
    [
        "ConnectionStrings:DefaultConnection",
        "Gmail:Address",
        "Gmail:AppPassword"
    ];

    [Theory]
    [MemberData(nameof(RequiredConfigurationKeys))]
    public void Validate_MissingRequiredConfiguration_ThrowsInvalidOperationException(string missingKey)
    {
        var values = CreateValidConfigurationValues();
        values[missingKey] = " ";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration));

        Assert.Contains(missingKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllRequiredConfigurationExists_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateValidConfigurationValues())
            .Build();

        ProductionConfigurationValidator.Validate(configuration);
    }

    private static Dictionary<string, string?> CreateValidConfigurationValues() =>
        new()
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=db;Port=5432;Database=wildlife;Username=wildlife",
            ["Gmail:Address"] = "test@example.invalid",
            ["Gmail:AppPassword"] = string.Concat("test", "-", "only")
        };
}
