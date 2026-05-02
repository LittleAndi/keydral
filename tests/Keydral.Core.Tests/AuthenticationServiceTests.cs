using System.Security.Claims;
using System.Text.Json;
using Keydral.Core.Authentication;
using Keydral.Core.Authorization;
using Keydral.Storage.Repositories;

namespace Keydral.Core.Tests;

/// <summary>
/// Tests for authentication service (JWT claim extraction).
/// </summary>
public class AuthenticationServiceTests
{
    private readonly IAuthenticationService _authenticationService = new AuthenticationService();

    [Fact]
    public void ExtractUserContext_WithValidClaims_ReturnsUserContext()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("preferred_username", "john.doe"),
            new Claim("email", "john@example.com"),
            new Claim("name", "John Doe"),
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { "user", "admin" } }))
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        var userContext = _authenticationService.ExtractUserContext(claimsPrincipal);

        // Assert
        Assert.NotNull(userContext);
        Assert.Equal("user-123", userContext.Id);
        Assert.Equal("john.doe", userContext.Username);
        Assert.Equal("john@example.com", userContext.Email);
        Assert.Equal("John Doe", userContext.DisplayName);
        Assert.Contains("user", userContext.RealmRoles);
        Assert.Contains("admin", userContext.RealmRoles);
    }

    [Fact]
    public void ExtractUserContext_WithMissingRequiredClaims_ReturnsNull()
    {
        // Arrange - missing 'sub' claim
        var claims = new[]
        {
            new Claim("preferred_username", "john.doe")
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        var userContext = _authenticationService.ExtractUserContext(claimsPrincipal);

        // Assert
        Assert.Null(userContext);
    }

    [Fact]
    public void ExtractUserContext_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var userContext = _authenticationService.ExtractUserContext(claimsPrincipal);

        // Assert
        Assert.Null(userContext);
    }

    [Fact]
    public void HasRole_WithMatchingRole_ReturnsTrue()
    {
        // Arrange
        var userContext = new UserContext
        {
            Id = "user-123",
            Username = "john",
            RealmRoles = new List<string> { "user", "admin" }
        };

        // Act
        var hasRole = _authenticationService.HasRole(userContext, "admin");

        // Assert
        Assert.True(hasRole);
    }

    [Fact]
    public void HasRole_WithNonMatchingRole_ReturnsFalse()
    {
        // Arrange
        var userContext = new UserContext
        {
            Id = "user-123",
            Username = "john",
            RealmRoles = new List<string> { "user" }
        };

        // Act
        var hasRole = _authenticationService.HasRole(userContext, "admin");

        // Assert
        Assert.False(hasRole);
    }

    [Fact]
    public void IsInGroup_WithMatchingGroup_ReturnsTrue()
    {
        // Arrange
        var userContext = new UserContext
        {
            Id = "user-123",
            Username = "john",
            Groups = new List<string> { "team-a", "team-b" }
        };

        // Act
        var isInGroup = _authenticationService.IsInGroup(userContext, "team-a");

        // Assert
        Assert.True(isInGroup);
    }

    [Fact]
    public void IsInGroup_WithNonMatchingGroup_ReturnsFalse()
    {
        // Arrange
        var userContext = new UserContext
        {
            Id = "user-123",
            Username = "john",
            Groups = new List<string> { "team-a" }
        };

        // Act
        var isInGroup = _authenticationService.IsInGroup(userContext, "team-c");

        // Assert
        Assert.False(isInGroup);
    }
}

/// <summary>
/// Tests for RBAC policy engine.
/// </summary>
public class RbacPolicyEngineTests
{
    [Fact]
    public async Task EvaluateAsync_WithMissingPrincipalId_ReturnsDeny()
    {
        // Arrange
        var mockPolicyRepository = new MockPolicyRepository();
        var policyEngine = new RbacPolicyEngine(mockPolicyRepository);

        // Act
        var decision = await policyEngine.EvaluateAsync("", "/secret-path", "secrets:read");

        // Assert
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingResourcePath_ReturnsDeny()
    {
        // Arrange
        var mockPolicyRepository = new MockPolicyRepository();
        var policyEngine = new RbacPolicyEngine(mockPolicyRepository);

        // Act
        var decision = await policyEngine.EvaluateAsync("user-123", "", "secrets:read");

        // Assert
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingAction_ReturnsDeny()
    {
        // Arrange
        var mockPolicyRepository = new MockPolicyRepository();
        var policyEngine = new RbacPolicyEngine(mockPolicyRepository);

        // Act
        var decision = await policyEngine.EvaluateAsync("user-123", "/secret-path", "");

        // Assert
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task CanPerformAsync_WithValidPolicy_ReturnsTrue()
    {
        // Arrange
        var mockPolicyRepository = new MockPolicyRepository();
        var policyEngine = new RbacPolicyEngine(mockPolicyRepository);

        // Add a test policy that allows
        mockPolicyRepository.AddTestPolicy(
            principal: "user-123",
            resourcePattern: "/team-a/*",
            action: "secrets:read",
            effect: "Allow"
        );

        // Act
        var canPerform = await policyEngine.CanPerformAsync("user-123", "/team-a/secret", "secrets:read");

        // Assert
        Assert.True(canPerform);
    }

    [Fact]
    public async Task CanPerformAsync_WithoutPolicy_ReturnsFalse()
    {
        // Arrange
        var mockPolicyRepository = new MockPolicyRepository();
        var policyEngine = new RbacPolicyEngine(mockPolicyRepository);

        // Act
        var canPerform = await policyEngine.CanPerformAsync("user-123", "/team-a/secret", "secrets:read");

        // Assert
        Assert.False(canPerform);
    }
}

/// <summary>
/// Mock policy repository for testing.
/// </summary>
public class MockPolicyRepository : IPolicyRepository
{
    private readonly List<Storage.Entities.Policy> _policies = new();

    public void AddTestPolicy(string principal, string resourcePattern, string action, string effect)
    {
        _policies.Add(new Storage.Entities.Policy
        {
            Id = Guid.NewGuid(),
            Name = $"test-policy-{_policies.Count}",
            Principal = principal,
            ResourcePattern = resourcePattern,
            Actions = action,
            Effect = effect,
            IsEnabled = true,
            IsDeleted = false
        });
    }

    public Task<Storage.Entities.Policy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.FirstOrDefault(p => p.Id == id));

    public Task<IEnumerable<Storage.Entities.Policy>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.AsEnumerable());

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.Any(p => p.Id == id));

    public Task<Storage.Entities.Policy> AddAsync(Storage.Entities.Policy entity, CancellationToken cancellationToken = default)
    {
        _policies.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Storage.Entities.Policy> UpdateAsync(Storage.Entities.Policy entity, CancellationToken cancellationToken = default)
    {
        var existing = _policies.FirstOrDefault(p => p.Id == entity.Id);
        if (existing != null)
        {
            _policies.Remove(existing);
            _policies.Add(entity);
        }
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = _policies.FirstOrDefault(p => p.Id == id);
        if (existing != null)
            _policies.Remove(existing);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public Task<Storage.Entities.Policy?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.FirstOrDefault(p => p.Name == name));

    public Task<IEnumerable<Storage.Entities.Policy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.Where(p => p.IsEnabled && !p.IsDeleted));

    public Task<IEnumerable<Storage.Entities.Policy>> GetPoliciesByPrincipalAsync(string principal, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.Where(p => p.Principal == principal && p.IsEnabled && !p.IsDeleted));

    public Task<IEnumerable<Storage.Entities.Policy>> GetPoliciesByResourcePatternAsync(string resourcePattern, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.Where(p => p.ResourcePattern == resourcePattern && p.IsEnabled && !p.IsDeleted));

    public Task<IEnumerable<Storage.Entities.Policy>> GetApplicablePoliciesAsync(string principal, string resource, CancellationToken cancellationToken = default)
    {
        var allPolicies = _policies.Where(p => p.IsEnabled && !p.IsDeleted).ToList();

        return Task.FromResult(allPolicies
            .Where(p =>
            {
                if (p.Principal != principal && p.Principal != "*")
                    return false;

                return MatchesResourcePattern(resource, p.ResourcePattern);
            })
            .AsEnumerable());
    }

    public Task<IEnumerable<Storage.Entities.Policy>> GetPoliciesByEffectAsync(string effect, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.Where(p => p.Effect == effect && p.IsEnabled && !p.IsDeleted));

    public Task<bool> CanPerformActionAsync(string principal, string action, string resource, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    private static bool MatchesResourcePattern(string resource, string pattern)
    {
        if (pattern == "*")
            return true;

        var regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*");

        var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$");
        return regex.IsMatch(resource);
    }
}
