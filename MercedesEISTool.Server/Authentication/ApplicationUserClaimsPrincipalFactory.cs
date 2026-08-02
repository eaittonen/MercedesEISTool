using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Authentication;

public sealed class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        identity.AddClaim(new Claim("DisplayName", user.DisplayName));
        identity.AddClaim(new Claim("OrganizationId", user.OrganizationId ?? string.Empty));

        foreach (var role in await UserManager.GetRolesAsync(user))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return identity;
    }
}
