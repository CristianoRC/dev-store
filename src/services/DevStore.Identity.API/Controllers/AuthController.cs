using DevStore.Core.Messages.Integration;
using DevStore.Identity.API.Models;
using DevStore.Identity.API.Services;
using DevStore.MessageBus;
using DevStore.WebAPI.Core.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace DevStore.Identity.API.Controllers
{
    [Route("api/identity")]
    public class AuthController : MainController
    {
        private readonly AuthenticationDevStoreService _authenticatioDevStoreService;
        private readonly IMessageBus _bus;

        public AuthController(
            AuthenticationDevStoreService authenticatioDevStoreService,
            IMessageBus bus)
        {
            _authenticatioDevStoreService = authenticatioDevStoreService;
            _bus = bus;
        }

        [HttpPost("new-account")]
        public async Task<ActionResult> Register(NewUser newUser)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var user = new IdentityUser
            {
                UserName = newUser.Email,
                Email = newUser.Email,
                EmailConfirmed = true
            };

            var result = await _authenticatioDevStoreService.UserManager.CreateAsync(user, newUser.Password);

            if (result.Succeeded)
            {
                var customerResult = await RegisterUser(newUser);

                if (!customerResult.ValidationResult.IsValid)
                {
                    await _authenticatioDevStoreService.UserManager.DeleteAsync(user);
                    return CustomResponse(customerResult.ValidationResult);
                }

                return CustomResponse(await _authenticatioDevStoreService.GenerateJwt(newUser.Email));
            }

            foreach (var error in result.Errors)
            {
                AddErrorToStack(error.Description);
            }

            return CustomResponse();
        }

        [HttpPost("auth")]
        public async Task<ActionResult> Login(UserLogin userLogin)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await _authenticatioDevStoreService.SignInManager.PasswordSignInAsync(userLogin.Email, userLogin.Password,
                false, true);

            if (result.Succeeded)
            {
                return CustomResponse(await _authenticatioDevStoreService.GenerateJwt(userLogin.Email));
            }

            if (result.IsLockedOut)
            {
                AddErrorToStack("User temporary blocked. Too many tries.");
                return CustomResponse();
            }

            AddErrorToStack("User or Password incorrect");
            return CustomResponse();
        }

        private async Task<ResponseMessage> RegisterUser(NewUser newUser)
        {
            var user = await _authenticatioDevStoreService.UserManager.FindByEmailAsync(newUser.Email);

            var userRegistered = new UserRegisteredIntegrationEvent(Guid.Parse(user.Id), newUser.Name, newUser.Email, newUser.SocialNumber);

            try
            {
                return await _bus.RequestAsync<UserRegisteredIntegrationEvent, ResponseMessage>(userRegistered);
            }
            catch (Exception)
            {
                await _authenticatioDevStoreService.UserManager.DeleteAsync(user);
                throw;
            }
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult> RefreshToken([FromBody] string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                AddErrorToStack("Invalid Refresh Token");
                return CustomResponse();
            }

            var token = await _authenticatioDevStoreService.ValidateRefreshToken(refreshToken);

            if (!token.IsValid)
            {
                AddErrorToStack("Expired Refresh Token");
                return CustomResponse();
            }

            return CustomResponse(await _authenticatioDevStoreService.GenerateJwt(token.Claims[JwtRegisteredClaimNames.Sub].ToString()));
        }
    }
}