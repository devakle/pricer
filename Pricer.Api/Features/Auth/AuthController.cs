using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Pricer.Api.Common.Api;

namespace Pricer.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] AdminLoginRequest req, [FromServices] IConfiguration config)
    {
        var adminSection = config.GetSection("Admin");
        var adminUser = adminSection["Username"];
        var adminPass = adminSection["Password"];
        var adminUserId = adminSection["UserId"];

        if (string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(adminPass) || string.IsNullOrWhiteSpace(adminUserId))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_admin", "Admin no configurado."));

        if (!string.Equals(req.Username, adminUser, StringComparison.Ordinal) ||
            !string.Equals(req.Password, adminPass, StringComparison.Ordinal))
        {
            return Unauthorized(ApiResponse<AdminLoginResponse>.Failure("unauthorized.invalid_credentials", "Credenciales inválidas."));
        }

        var jwtSection = config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var key = jwtSection["Key"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_jwt", "JWT no configurado."));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, adminUserId),
            new Claim(ClaimTypes.Name, adminUser),
            new Claim(ClaimTypes.NameIdentifier, adminUserId),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(4);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(ApiResponse<AdminLoginResponse>.Success(new AdminLoginResponse(tokenValue, expiresAt)));
    }

    [HttpPost("user-login")]
    public IActionResult UserLogin([FromBody] AdminLoginRequest req, [FromServices] IConfiguration config)
    {
        var userSection = config.GetSection("User");
        var username = userSection["Username"];
        var password = userSection["Password"];
        var userId = userSection["UserId"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(userId))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_user", "Usuario no configurado."));

        if (!string.Equals(req.Username, username, StringComparison.Ordinal) ||
            !string.Equals(req.Password, password, StringComparison.Ordinal))
        {
            return Unauthorized(ApiResponse<AdminLoginResponse>.Failure("unauthorized.invalid_credentials", "Credenciales invalidas."));
        }

        var jwtSection = config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var key = jwtSection["Key"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_jwt", "JWT no configurado."));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(ApiResponse<AdminLoginResponse>.Success(new AdminLoginResponse(tokenValue, expiresAt)));
    }

    [HttpPost("merchant-login")]
    public IActionResult MerchantLogin([FromBody] AdminLoginRequest req, [FromServices] IConfiguration config)
    {
        var merchantSection = config.GetSection("Merchant");
        var username = merchantSection["Username"];
        var password = merchantSection["Password"];
        var userId = merchantSection["UserId"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(userId))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_merchant", "Comerciante no configurado."));

        if (!string.Equals(req.Username, username, StringComparison.Ordinal) ||
            !string.Equals(req.Password, password, StringComparison.Ordinal))
        {
            return Unauthorized(ApiResponse<AdminLoginResponse>.Failure("unauthorized.invalid_credentials", "Credenciales invalidas."));
        }

        var jwtSection = config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var key = jwtSection["Key"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key))
            return StatusCode(500, ApiResponse<AdminLoginResponse>.Failure("config.missing_jwt", "JWT no configurado."));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "Merchant"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(6);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(ApiResponse<AdminLoginResponse>.Success(new AdminLoginResponse(tokenValue, expiresAt)));
    }
}
