using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A UsersController")]
[TestFixture]
public class UsersControllerTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static UsersController CreateController(CampfireDbContext db, Guid actingUserId)
    {
        var controller = new UsersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())], "test")),
            },
        };
        return controller;
    }

    [Test]
    public async Task Me_ForAnExistingUser_ReturnsTheirOwnProfileFields()
    {
        await using var db = CreateDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "me@example.com",
            PasswordHash = "hash",
            DisplayName = "Me",
            Role = UserRole.Member,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await CreateController(db, user.Id).Me();

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        Assert.That(ok.Value, Is.Not.Null);
    }

    [Test]
    public async Task Me_ForAUserIdWithNoMatchingRecord_ReturnsNotFound()
    {
        await using var db = CreateDb();

        var result = await CreateController(db, Guid.NewGuid()).Me();

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    private static async Task<User> AddUserAsync(CampfireDbContext db, string email, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = "Original Name",
            Role = UserRole.Member,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task UpdateProfile_WithWrongCurrentPassword_IsUnauthorized()
    {
        await using var db = CreateDb();
        var user = await AddUserAsync(db, "me@example.com", "correct-password");

        var result = await CreateController(db, user.Id).UpdateProfile(
            new UpdateProfileRequest("New Name", "new@example.com", "wrong-password"));

        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That((await db.Users.FindAsync(user.Id))!.DisplayName, Is.EqualTo("Original Name"));
    }

    [Test]
    public async Task UpdateProfile_ToAnEmailAlreadyInUse_ReturnsConflict()
    {
        await using var db = CreateDb();
        await AddUserAsync(db, "taken@example.com", "irrelevant");
        var user = await AddUserAsync(db, "me@example.com", "correct-password");

        var result = await CreateController(db, user.Id).UpdateProfile(
            new UpdateProfileRequest("New Name", "taken@example.com", "correct-password"));

        Assert.That(result, Is.InstanceOf<ConflictObjectResult>());
    }

    [Test]
    public async Task UpdateProfile_WithCorrectPassword_UpdatesNameAndEmail()
    {
        await using var db = CreateDb();
        var user = await AddUserAsync(db, "me@example.com", "correct-password");

        var result = await CreateController(db, user.Id).UpdateProfile(
            new UpdateProfileRequest("New Name", "new@example.com", "correct-password"));

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var updated = await db.Users.FindAsync(user.Id);
        Assert.That(updated!.DisplayName, Is.EqualTo("New Name"));
        Assert.That(updated.Email, Is.EqualTo("new@example.com"));
    }

    [Test]
    public async Task ChangePassword_WithWrongCurrentPassword_IsUnauthorized()
    {
        await using var db = CreateDb();
        var user = await AddUserAsync(db, "me@example.com", "correct-password");

        var result = await CreateController(db, user.Id).ChangePassword(
            new ChangePasswordRequest("wrong-password", "a-new-password123"));

        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task ChangePassword_WithCorrectCurrentPassword_ActuallyChangesTheStoredHash()
    {
        await using var db = CreateDb();
        var user = await AddUserAsync(db, "me@example.com", "correct-password");

        var result = await CreateController(db, user.Id).ChangePassword(
            new ChangePasswordRequest("correct-password", "a-new-password123"));

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        var updated = await db.Users.FindAsync(user.Id);
        Assert.That(BCrypt.Net.BCrypt.Verify("a-new-password123", updated!.PasswordHash), Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("correct-password", updated.PasswordHash), Is.False);
    }
}
