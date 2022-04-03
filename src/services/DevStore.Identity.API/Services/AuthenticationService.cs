using DevStore.Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DevStore.WebAPI.Core.User;
using Microsoft.IdentityModel.JsonWebTokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace DevStore.Identity.API.Services;

public class AuthenticationDevStoreService
{
    public readonly SignInManager<IdentityUser> SignInManager;
    public readonly UserManager<IdentityUser> UserManager;

    private readonly IJwtService _jwksService;
    private readonly IAspNetUser _aspNetUser;

    public AuthenticationDevStoreService(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IJwtService jwksService,
        IAspNetUser aspNetUser)
    {
        SignInManager = signInManager;
        UserManager = userManager;
        _jwksService = jwksService;
        _aspNetUser = aspNetUser;
    }

    public async Task<UserLoginResponse> GenerateJwt(string email)
    {
        var user = await UserManager.FindByEmailAsync(email);
        var claims = await UserManager.GetClaimsAsync(user);

        var identityClaims = await GetUserClaims(claims, user);
        var encodedToken = await GenerateToken(identityClaims);

        var refreshToken = await GenerateRefreshToken(user.Email);

        return ObterRespostaToken(encodedToken, user, claims, refreshToken);
    }

    private async Task<ClaimsIdentity> GetUserClaims(ICollection<Claim> claims, IdentityUser user)
    {
        var userRoles = await UserManager.GetRolesAsync(user);

        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id));
        claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        foreach (var userRole in userRoles)
        {
            claims.Add(new Claim("role", userRole));
        }

        var identityClaims = new ClaimsIdentity();
        identityClaims.AddClaims(claims);

        return identityClaims;
    }

    private async Task<string> GenerateToken(ClaimsIdentity identityClaims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var currentIssuer =
            $"{_aspNetUser.GetHttpContext().Request.Scheme}://{_aspNetUser.GetHttpContext().Request.Host}";
        var key = await _jwksService.GetCurrentSigningCredentials();
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = currentIssuer,
            Subject = identityClaims,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = key
        });

        return tokenHandler.WriteToken(token);
    }

    private async Task<string> GenerateRefreshToken(string email)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, email) };
        var currentIssuer = $"{_aspNetUser.GetHttpContext().Request.Scheme}://{_aspNetUser.GetHttpContext().Request.Host}";

        // Em cenários produtivos use esse
        var key = await _jwksService.GetCurrentSigningCredentials();

        var identityClaims = new ClaimsIdentity();
        identityClaims.AddClaims(claims);

        var handler = new JwtSecurityTokenHandler();
        var securityToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = currentIssuer,
            Subject = identityClaims,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(30),
            TokenType = "rt+jwt",
            SigningCredentials = key,
        });

        return handler.WriteToken(securityToken);
    }

    private UserLoginResponse ObterRespostaToken(string encodedToken, IdentityUser user, IEnumerable<Claim> claims, string refreshToken)
    {
        return new UserLoginResponse
        {
            AccessToken = encodedToken,
            RefreshToken = refreshToken,
            ExpiresIn = TimeSpan.FromHours(1).TotalSeconds,
            UserToken = new UserToken
            {
                Id = user.Id,
                Email = user.Email,
                Claims = claims.Select(c => new UserClaim { Type = c.Type, Value = c.Value })
            }
        };
    }


    public async Task<TokenValidationResult> ValidateRefreshToken(string token)
    {
        var key = await _jwksService.GetCurrentSigningCredentials();
        var currentIssuer = $"{_aspNetUser.GetHttpContext().Request.Scheme}://{_aspNetUser.GetHttpContext().Request.Host}";
        var handler = new JsonWebTokenHandler();
        var result = handler.ValidateToken(token, new TokenValidationParameters()
        {
            ValidIssuer = currentIssuer,
            ValidateIssuer = true,
            ValidateAudience = false,
            IssuerSigningKey = key.Key,
        });

        return result;
    }
}