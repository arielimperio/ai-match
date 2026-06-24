using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// JWT Authentication
var key = System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key)
        };
    });


builder.Services.AddScoped<NackaMatchmaking.API.Services.MatchingService>();
builder.Services.AddScoped<NackaMatchmaking.API.Services.IEmailService, NackaMatchmaking.API.Services.SendGridEmailService>();
builder.Services.AddHttpClient<NackaMatchmaking.API.Services.IAiMatchingService, NackaMatchmaking.API.Services.AiMatchingService>(client => {
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<NackaMatchmaking.API.Services.TaskProgressService>();

// Named client used by AdminMatchingController for CSV batch scoring.
// The default 100-second timeout is too short for large batches; use 5 minutes.
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

// Ensure the system company row exists (required for system-wide settings FK)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Data.ApplicationDbContext>();
    var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    if (!db.Companies.Any(c => c.Id == systemId))
    {
        db.Companies.Add(new NackaMatchmaking.API.Models.Company
        {
            Id = systemId,
            Name = "System Settings",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
