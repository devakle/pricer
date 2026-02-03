using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Pricer.Infrastructure.Persistence;
using Pricer.Application.Pricing.Create;
using Pricer.Application.Stores.GetNear;
using Pricer.Infrastructure.Repositories.Catalog;
using Pricer.Infrastructure.Repositories.Pricing;
using Pricer.Infrastructure.Repositories.Stores;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pricer API", Version = "v1" });
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };
    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    var jwtSecuritySchemeReference = new OpenApiSecuritySchemeReference("Bearer", null, null);
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            jwtSecuritySchemeReference,
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        x => x.UseNetTopologySuite());
    opt.EnableSensitiveDataLogging();
    opt.LogTo(Console.WriteLine);
});


builder.Services.AddScoped<GetStoresNearHandler>();
builder.Services.AddScoped<CreatePriceReportHandler>();

builder.Services.AddScoped<IStoreReadRepository, StoreReadRepository>();
builder.Services.AddScoped<IStoreExistsChecker, StoreExistsChecker>();
builder.Services.AddScoped<ISkuExistsChecker, SkuExistsChecker>();
builder.Services.AddScoped<IPriceReportRepository, PriceReportRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.ScrapingBeeOptions>(
    builder.Configuration.GetSection("ScrapingBee"));

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.ScrapeGraphOptions>(
    builder.Configuration.GetSection("ScrapeGraph"));

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.PlaywrightOptions>(
    builder.Configuration.GetSection("Playwright"));

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.AmazonPlaywrightOptions>(
    builder.Configuration.GetSection("AmazonPlaywright"));

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.AliExpressPlaywrightOptions>(
    builder.Configuration.GetSection("AliExpressPlaywright"));

builder.Services.Configure<Pricer.Api.Features.ExternalProducts.ExternalSearchCacheOptions>(
    builder.Configuration.GetSection("ExternalSearchCache"));

var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddHttpClient<Pricer.Api.Features.ExternalProducts.ScrapingBeeSearchClient>(client =>
{
    client.BaseAddress = new Uri("https://app.scrapingbee.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "Pricer/1.0");
    client.Timeout = TimeSpan.FromSeconds(140);
});

builder.Services.AddSingleton<Pricer.Api.Features.ExternalProducts.ScrapeGraphSearchClient>();
builder.Services.AddSingleton<Pricer.Api.Features.ExternalProducts.PlaywrightSearchClient>();
builder.Services.AddSingleton<Pricer.Api.Features.ExternalProducts.AmazonPlaywrightSearchClient>();
builder.Services.AddSingleton<Pricer.Api.Features.ExternalProducts.AliExpressPlaywrightSearchClient>();

var jwt = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwt["Key"]!);



builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
    options.AddPolicy("MerchantOnly", policy => policy.RequireRole("Merchant"));
    options.AddPolicy("CanReportPrice", policy => policy.RequireRole("User", "Admin", "Merchant"));
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(15);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(ex, "Database migration failed (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s...", attempt, maxAttempts, delay.TotalSeconds);
            Thread.Sleep(delay);
        }
    }
}

app.UseCors("DevCors");
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
