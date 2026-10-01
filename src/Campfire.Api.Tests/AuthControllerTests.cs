using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests;

[TestFixture]
public class AuthControllerTests
{
    private static AuthController CreateController(CampfireDbContext db)
    {
        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long",
            ExpiryMinutes = 60,
        });

        return new AuthController(db, new JwtTokenService(jwtOptions));
    }

    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    [Test]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("creator@example.com", "password123", "Creator One", UserRole.Creator));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.InstanceOf<AuthResponse>());
        var response = (AuthResponse)ok.Value!;
        Assert.That(response.Role, Is.EqualTo(UserRole.Creator));
        Assert.That(string.IsNullOrEmpty(response.Token), Is.False);
        Assert.That(db.Users, Has.Exactly(1).Items);
    }

    [Test]
    public async Task Register_RejectsAdminRole()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("admin@example.com", "password123", "Admin", UserRole.Admin));

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(db.Users, Is.Empty);
    }

    [Test]
    public async Task Register_RejectsDuplicateEmail()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Register(new RegisterRequest("member@example.com", "password456", "Member Two", UserRole.Member));

        Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
        Assert.That(db.Users, Has.Exactly(1).Items);
    }

    [Test]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Login(new LoginRequest("member@example.com", "password123"));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.InstanceOf<AuthResponse>());
    }

    [Test]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Login(new LoginRequest("member@example.com", "wrong-password"));

        Assert.That(result.Result, Is.InstanceOf<UnauthorizedObjectResult>());
    }
}
