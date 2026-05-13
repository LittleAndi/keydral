using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace Keydral.API.Tests.Utilities;

/// <summary>
/// Generates self-signed JWTs for integration tests so that requests can pass through
/// the JWT Bearer middleware without a live Keycloak server.
/// The signing key is also exposed so test WebApplicationFactory instances can configure
/// JwtBearerOptions to validate these tokens locally.
/// </summary>
internal static class TestJwtTokenFactory
{
    /// <summary>
    /// Symmetric signing key shared between token generation and JWT bearer validation.
    /// NOT for production use.
    /// </summary>
    internal static readonly SymmetricSecurityKey SigningKey =
        new(System.Text.Encoding.UTF8.GetBytes("keydral-test-signing-key-must-be-32-chars!!"));

    /// <summary>
    /// Creates a signed JWT token with the given <paramref name="userId"/> as the
    /// <c>sub</c> claim. The token is valid for 1 hour.
    /// </summary>
    internal static string CreateToken(string userId, params string[] roles)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("preferred_username", userId),
        };

        if (roles.Length > 0)
        {
            claims.Add(new Claim("realm_access", JsonSerializer.Serialize(new { roles })));
        }

        var token = new JwtSecurityToken(
            issuer: "keydral-test",
            audience: "keydral-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
