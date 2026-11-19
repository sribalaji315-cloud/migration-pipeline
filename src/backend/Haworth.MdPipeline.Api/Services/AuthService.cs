using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Haworth.MdPipeline.Api.Services;

public sealed class AuthService(UserManager<AppUser> userManager, IConfiguration configuration)
{
    public async Task<AuthLoginResponse> LoginAsync(AuthLoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new DomainValidationException("Invalid email or password.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new DomainValidationException("Invalid email or password.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthLoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Email ?? string.Empty, user.DisplayName, roles.ToList());
    }
}
