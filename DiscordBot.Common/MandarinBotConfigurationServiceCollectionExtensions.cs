using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiscordBot;

public static class MandarinBotConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddMandarinBotConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(MandarinBotOptions.SectionName);
        services.AddSingleton<IValidateOptions<MandarinBotOptions>>(
            new MandarinBotOptionsValidator(environment.IsProduction()));
        services.AddOptions<MandarinBotOptions>()
            .Bind(section)
            .ValidateOnStart();

        services.AddSingleton(
            services => services
                .GetRequiredService<IOptions<MandarinBotOptions>>()
                .Value
                .Discord);
        services.AddSingleton(
            services => services
                .GetRequiredService<IOptions<MandarinBotOptions>>()
                .Value
                .FantasyPremierLeague);
        services.AddSingleton(
            services => services
                .GetRequiredService<IOptions<MandarinBotOptions>>()
                .Value
                .Schedules);
        services.AddSingleton(
            services => services
                .GetRequiredService<IOptions<MandarinBotOptions>>()
                .Value
                .Notifications);
        services.AddSingleton(
            services => services
                .GetRequiredService<IOptions<MandarinBotOptions>>()
                .Value
                .WelcomeMessages);

        return services;
    }
}
