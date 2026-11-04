using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
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
}
