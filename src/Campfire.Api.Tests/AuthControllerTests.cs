using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Campfire.Api.Tests;

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

    [Fact]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("creator@example.com", "password123", "Creator One", UserRole.Creator));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.Equal(UserRole.Creator, response.Role);
        Assert.False(string.IsNullOrEmpty(response.Token));
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Register_RejectsAdminRole()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("admin@example.com", "password123", "Admin", UserRole.Admin));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Register(new RegisterRequest("member@example.com", "password456", "Member Two", UserRole.Member));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Login(new LoginRequest("member@example.com", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AuthResponse>(ok.Value);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        await controller.Register(new RegisterRequest("member@example.com", "password123", "Member One", UserRole.Member));

        var result = await controller.Login(new LoginRequest("member@example.com", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
