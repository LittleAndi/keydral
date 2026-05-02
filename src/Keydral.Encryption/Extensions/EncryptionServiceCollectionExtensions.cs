using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Keydral.Encryption.Configuration;
using Keydral.Encryption.Providers;

namespace Keydral.Encryption.Extensions;

/// <summary>
/// Extension methods for registering encryption services in dependency injection.
/// </summary>
public static class EncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Add encryption services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddEncryption(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load configuration
        var options = new EncryptionOptions();
        configuration.GetSection("Encryption").Bind(options);

        services.AddSingleton(options);

        // Register master key provider based on configuration.
        // Validation is deferred to first resolution so that WebApplicationFactory
        // can replace this registration in tests before it is ever invoked.
        services.AddSingleton<IMasterKeyProvider>(provider =>
        {
            options.Validate();
            return options.Provider.ToLowerInvariant() switch
            {
                "file" => new FileBasedMasterKeyProvider(options.MasterKeyFilePath!),
                "kubernetes" => new KubernetesSecretMasterKeyProvider(
                    options.KubernetesSecretPath ?? "/var/run/secrets/keydral/master-key"),
                "none" => throw new InvalidOperationException(
                    "No encryption provider configured. Set Encryption:Provider in configuration."),
                _ => throw new InvalidOperationException(
                    $"Unknown encryption provider: {options.Provider}")
            };
        });

        // Register encryption service
        services.AddSingleton<IEncryptionService, EnvelopeEncryptionService>();

        return services;
    }

    /// <summary>
    /// Add encryption services with explicit master key provider.
    /// </summary>
    public static IServiceCollection AddEncryption(
        this IServiceCollection services,
        IMasterKeyProvider masterKeyProvider,
        EncryptionOptions? options = null)
    {
        options ??= new EncryptionOptions();
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton(masterKeyProvider);
        services.AddSingleton<IEncryptionService, EnvelopeEncryptionService>();

        return services;
    }
}
