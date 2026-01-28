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
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
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

app.UseCors("DevCors");
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
