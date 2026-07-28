using Microsoft.Extensions.Configuration;
using Webshop.Data.Services;

namespace Webshop.Web.Tests;

public class AccountServiceTests
{
    [Fact]
    public void Registering_Account_ShouldBeAvailableInLaterServiceInstances()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);
        var config = builder.Build();

        var firstService = new MongoAccountService(config);
        var email = $"ada+{Guid.NewGuid():N}@example.com";
        var registered = firstService.Register(
            "Ada",
            "Lovelace",
            "+45 12 34 56 78",
            email,
            "StrongPass123",
            "Examplevej 12",
            "2100",
            "København",
            out var registerError);

        Assert.True(registered);
        Assert.Equal(string.Empty, registerError);

        var secondService = new MongoAccountService(config);
        var loginSucceeded = secondService.Login(email, "StrongPass123", out var loginError);

        Assert.True(loginSucceeded);
        Assert.Equal(string.Empty, loginError);
    }

    [Fact]
    public void Login_WithAdminCredentials_ReturnsAdminAccount()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);
        var config = builder.Build();

        var service = new MongoAccountService(config);

        var success = service.Login("admin", "admin", out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        Assert.True(service.CurrentAccount?.IsAdmin);
    }

    [Fact]
    public void Login_WithAdminEmailAlias_ReturnsAdminAccount()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);
        var config = builder.Build();

        var service = new MongoAccountService(config);

        var success = service.Login("admin@admin.dk", "admin", out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        Assert.True(service.CurrentAccount?.IsAdmin);
    }
}
