using System.Text;
using Campfire.Api.Data;
using Campfire.Api.Options;
using Campfire.Api.Payments;
using Campfire.Api.Services;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<CampfireDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
StripeConfiguration.ApiKey = builder.Configuration[$"{StripeOptions.SectionName}:SecretKey"];
builder.Services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection(AdminSeedOptions.SectionName));

builder.Services.Configure<CdnOptions>(builder.Configuration.GetSection(CdnOptions.SectionName));
builder.Services.AddHttpClient<BunnyMediaStorage>();
builder.Services.AddScoped<LocalMediaStorage>();
builder.Services.AddScoped<IMediaStorage>(sp =>
{
    var cdnOptions = sp.GetRequiredService<IOptions<CdnOptions>>().Value;
    return string.Equals(cdnOptions.Provider, "Bunny", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<BunnyMediaStorage>()
        : sp.GetRequiredService<LocalMediaStorage>();
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CreatorOnly", policy => policy.RequireRole("Creator"));
    options.AddPolicy("MemberOnly", policy => policy.RequireRole("Member"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CampfireDbContext>();
    var adminSeedOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>();
    await AdminSeeder.EnsureAdminSeededAsync(db, adminSeedOptions);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// No app.UseStaticFiles(): media under wwwroot/media is served only through
// MediaController, which enforces the signed-URL check. Wiring the static
// files middleware here would let anyone bypass that check entirely.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
