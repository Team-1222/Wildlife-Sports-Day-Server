namespace Wildlife_Sports_Day_Server.Infrastructure.Configuration;

public static class ProductionConfigurationValidator
{
    private const string DefaultConnectionName = "DefaultConnection";

    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ValidateRequiredValue(
            "ConnectionStrings:DefaultConnection",
            configuration.GetConnectionString(DefaultConnectionName));
        ValidateRequiredValue("Gmail:Address", configuration["Gmail:Address"]);
        ValidateRequiredValue("Gmail:AppPassword", configuration["Gmail:AppPassword"]);
    }

    private static void ValidateRequiredValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required production configuration is missing: {key}");
        }
    }
}
