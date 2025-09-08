using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroShop.Services.Identity.Domain;
using MicroShop.Services.Identity.Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Identity.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Db
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(o => { o.User.RequireUniqueEmail = true; })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
builder.Services.AddAuthentication().AddJwtBearer(o =>
{
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = key
    };
});
builder.Services.AddAuthorization();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("identity-service"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter());

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/v1/auth/register", async (UserManager<ApplicationUser> um, RegisterDto dto) =>
{
    var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = dto.Email, Email = dto.Email, EmailConfirmed = true };
    var res = await um.CreateAsync(user, dto.Password);
    return res.Succeeded ? Results.Created($"/api/v1/users/{user.Id}", new { user.Id, user.Email })
                         : Results.BadRequest(res.Errors);
});

app.MapPost("/api/v1/auth/login", async (UserManager<ApplicationUser> um, LoginDto dto) =>
{
    var user = await um.FindByEmailAsync(dto.Email);
    if (user is null || !await um.CheckPasswordAsync(user, dto.Password)) return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email!)
    };
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(issuer: jwtSection["Issuer"], audience: jwtSection["Audience"],
        claims: claims, expires: DateTime.UtcNow.AddHours(2), signingCredentials: creds);
    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { access_token = jwt });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
