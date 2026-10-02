using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using SmartHal.Server.Composition;
using Xunit;

namespace SmartHal.Server.UnitTests.Composition;

/// <summary>
/// Verifies that the composition root registers the contract validator once, through its abstraction, for the whole
/// process.
/// </summary>
public sealed class ValidationRegistrationTests
{
    [Fact]
    public void AddSmartHalValidation_EmptyCollection_RegistersTheValidatorAsSingletonThroughItsInterface()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddSmartHalValidation();

        // Assert
        returned.Should().BeSameAs(services);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IContractValidator))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ImplementationType == typeof(ContractValidator));
    }

    [Fact]
    public void AddSmartHalValidation_Resolved_ReturnsTheSameInstanceEveryTime()
    {
        // Arrange
        var services = new ServiceCollection().AddSmartHalValidation();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        // Act
        var first = provider.GetRequiredService<IContractValidator>();
        var second = provider.GetRequiredService<IContractValidator>();

        // Assert
        first.Should().BeOfType<ContractValidator>();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddSmartHalValidation_NullCollection_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddSmartHalValidation();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddSmartHalValidation_CalledTwice_KeepsASingleRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSmartHalValidation().AddSmartHalValidation();

        // Assert
        services.Where(descriptor => descriptor.ServiceType == typeof(IContractValidator)).Should().ContainSingle();
    }

    [Fact]
    public void AddSmartHalValidation_ValidatorRegisteredBefore_KeepsTheExistingRegistration()
    {
        // Arrange
        var replacement = A.Fake<IContractValidator>();
        var services = new ServiceCollection().AddSingleton(replacement);

        // Act
        services.AddSmartHalValidation();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IContractValidator>().Should().BeSameAs(replacement);
    }

    [Fact]
    public void AddSmartHalValidation_ResolvedFromSeveralScopes_ReturnsTheRootInstance()
    {
        // Arrange
        using var provider = new ServiceCollection().AddSmartHalValidation().BuildServiceProvider(validateScopes: true);
        var root = provider.GetRequiredService<IContractValidator>();

        // Act
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        // Assert
        firstScope.ServiceProvider.GetRequiredService<IContractValidator>().Should().BeSameAs(root);
        secondScope.ServiceProvider.GetRequiredService<IContractValidator>().Should().BeSameAs(root);
    }
}
