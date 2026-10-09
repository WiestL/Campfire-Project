using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests;

[TestFixture]
public class TiersControllerTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static async Task<User> AddCreatorAsync(CampfireDbContext db, string displayName = "Test Creator")
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = displayName,
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        return creator;
    }

    private static TiersController CreateController(CampfireDbContext db, Guid actingUserId)
    {
        var controller = new TiersController(db);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())], "test"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        return controller;
    }

    [Test]
    public async Task Create_AddsTier_AndReturnsIt()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateTierRequest("Fitness Vlog Access", "Weekly vlogs", 9.99m, 1));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (TierResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Name, Is.EqualTo("Fitness Vlog Access"));
        Assert.That(response.CreatorDisplayName, Is.EqualTo("Test Creator"));
        Assert.That(db.MembershipTiers, Has.Exactly(1).Items);
    }

    [Test]
    public async Task GetAll_ReturnsTiersOrderedByLevel()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        await controller.Create(new CreateTierRequest("Bronze", "desc", 5m, 0));

        var result = await controller.GetAll();

        var tiers = (List<TierResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(tiers, Has.Count.EqualTo(2));
        Assert.That(tiers[0].Name, Is.EqualTo("Bronze"));
        Assert.That(tiers[1].Name, Is.EqualTo("Gold"));
    }

    [Test]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetById_KnownId_ReturnsTier()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var created = await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        var result = await controller.GetById(tierId);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (TierResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Id, Is.EqualTo(tierId));
        Assert.That(response.Name, Is.EqualTo("Gold"));
    }

    [Test]
    public async Task Create_WithUnknownActingUser_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));

        Assert.That(result.Result, Is.InstanceOf<UnauthorizedResult>());
        Assert.That(db.MembershipTiers, Is.Empty);
    }

    [Test]
    public async Task Update_ByOwningCreator_Succeeds()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var created = await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        var result = await controller.Update(tierId, new UpdateTierRequest("Gold+", "new desc", 25m, 2));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (TierResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Name, Is.EqualTo("Gold+"));
        Assert.That(response.Price, Is.EqualTo(25m));
    }

    [Test]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Update(Guid.NewGuid(), new UpdateTierRequest("Gold", "desc", 20m, 2));

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Update_ByNonOwningCreator_ReturnsForbidden()
    {
        await using var db = CreateDb();
        var owner = await AddCreatorAsync(db, "Owner");
        var otherCreator = await AddCreatorAsync(db, "Other");
        var ownerController = CreateController(db, owner.Id);
        var created = await ownerController.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        var otherController = CreateController(db, otherCreator.Id);
        var result = await otherController.Update(tierId, new UpdateTierRequest("Hijacked", "desc", 1m, 0));

        Assert.That(result.Result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task Delete_ByOwningCreator_RemovesTier()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var created = await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        var result = await controller.Delete(tierId);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        Assert.That(db.MembershipTiers, Is.Empty);
    }

    [Test]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Delete(Guid.NewGuid());

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Delete_ByNonOwningCreator_ReturnsForbidden()
    {
        await using var db = CreateDb();
        var owner = await AddCreatorAsync(db, "Owner");
        var otherCreator = await AddCreatorAsync(db, "Other");
        var ownerController = CreateController(db, owner.Id);
        var created = await ownerController.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        var otherController = CreateController(db, otherCreator.Id);
        var result = await otherController.Delete(tierId);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
        Assert.That(db.MembershipTiers, Has.Exactly(1).Items);
    }

    [Test]
    public async Task Delete_WithActiveSubscriptions_ReturnsConflict()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db, "Owner");
        var member = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            PasswordHash = "hash",
            DisplayName = "A Member",
            Role = UserRole.Member,
        };
        db.Users.Add(member);
        var controller = CreateController(db, creator.Id);
        var created = await controller.Create(new CreateTierRequest("Gold", "desc", 20m, 2));
        var tierId = ((TierResponse)((OkObjectResult)created.Result!).Value!).Id;

        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tierId,
            Status = SubscriptionStatus.Active,
        });
        await db.SaveChangesAsync();

        var result = await controller.Delete(tierId);

        Assert.That(result, Is.InstanceOf<ConflictObjectResult>());
        Assert.That(db.MembershipTiers, Has.Exactly(1).Items);
    }
}
