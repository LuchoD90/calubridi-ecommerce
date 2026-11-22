using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Calubridi.Api.Store;

[ApiController]
[Route("api/account")]
public sealed class AccountController(
    UserManager<StoreUser> users, SignInManager<StoreUser> signIn,
    IConfiguration config) : ControllerBase
{
    public sealed record RegisterInput(
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(32, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9_.-]+$")] string Username,
        [Required, StringLength(128, MinimumLength = 12)] string Password);

    public sealed record LoginInput(
        [Required, StringLength(254)] string Login,
        [Required, StringLength(128)] string Password);

    [HttpPost("register"), EnableRateLimiting("accounts")]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        var user = new StoreUser { UserName = input.Username, Email = input.Email };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "No se pudo crear la cuenta. Verificá que el usuario y el correo estén disponibles y que la contraseña incluya mayúscula, minúscula y número." });

        await signIn.SignInAsync(user, isPersistent: false);
        return Ok();
    }

    [HttpPost("login"), EnableRateLimiting("accounts")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var user = input.Login.Contains('@')
            ? await users.FindByEmailAsync(input.Login)
            : await users.FindByNameAsync(input.Login);
        var result = user is null ? null : await signIn.PasswordSignInAsync(user, input.Password, false, lockoutOnFailure: true);
        return result?.Succeeded == true ? Ok() : Unauthorized(new { message = "No pudimos iniciar sesión con esos datos. Si probaste varias veces, esperá unos minutos." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    public sealed record LinkInput([Required, StringLength(128)] string Password);

    [HttpPost("google-link"), Authorize, EnableRateLimiting("accounts")]
    public async Task<IActionResult> LinkGoogle([FromForm] LinkInput input)
    {
        if (!GoogleEnabled) return NotFound();

        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var checkedPassword = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!checkedPassword.Succeeded) return Redirect("/?auth=link-password#cuenta");
        if ((await users.GetLoginsAsync(user)).Any(login => login.LoginProvider == "Google"))
            return Redirect("/?auth=google-linked#cuenta");

        var properties = signIn.ConfigureExternalAuthenticationProperties(
            "Google", Url.Action(nameof(GoogleLinkDone))!, user.Id);
        return Challenge(properties, "Google");
    }

    [HttpGet("google-link-done"), Authorize, EnableRateLimiting("accounts")]
    public async Task<IActionResult> GoogleLinkDone()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var info = await signIn.GetExternalLoginInfoAsync(user.Id);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (!VerifiedGoogle(info, out var email) || info is null)
            return Redirect("/?auth=link-error#cuenta");

        var currentOwner = await users.FindByEmailAsync(email!);
        if (currentOwner is not null && currentOwner.Id != user.Id)
            return Redirect("/?auth=link-used#cuenta");

        if ((await users.GetLoginsAsync(user)).Any(login => login.LoginProvider == "Google"))
            return Redirect("/?auth=google-linked#cuenta");

        var result = await users.AddLoginAsync(user, info);
        return Redirect(result.Succeeded ? "/?auth=google-linked#cuenta" : "/?auth=link-used#cuenta");
    }

    private bool GoogleEnabled =>
        !string.IsNullOrWhiteSpace(config["Google:ClientId"]) && !string.IsNullOrWhiteSpace(config["Google:ClientSecret"]);

    private static bool VerifiedGoogle(ExternalLoginInfo? info, out string? email)
    {
        email = info?.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        return info?.LoginProvider == "Google" && !string.IsNullOrWhiteSpace(email) &&
            string.Equals(info.Principal.FindFirst("google_email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("google"), EnableRateLimiting("accounts")]
    public IActionResult Google()
    {
        if (!GoogleEnabled)
            return NotFound();
        return Challenge(signIn.ConfigureExternalAuthenticationProperties("Google", Url.Action(nameof(GoogleDone))!), "Google");
    }

    [HttpGet("google-done"), EnableRateLimiting("accounts")]
    public async Task<IActionResult> GoogleDone()
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (!VerifiedGoogle(info, out var email) || info is null)
            return Redirect("/?auth=google-error#cuenta");

        var login = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false, bypassTwoFactor: false);
        if (login.Succeeded) return Redirect("/#cuenta");
        if (login.IsLockedOut || login.RequiresTwoFactor || login.IsNotAllowed)
            return Redirect("/?auth=google-error#cuenta");
        if (await users.FindByEmailAsync(email!) is not null)
            return Redirect("/?auth=email-exists#cuenta");

        var user = new StoreUser { UserName = "google-" + Guid.NewGuid().ToString("N"), Email = email, EmailConfirmed = true };
        if (!(await users.CreateAsync(user)).Succeeded) return Redirect("/?auth=google-error#cuenta");
        if (!(await users.AddLoginAsync(user, info)).Succeeded)
        {
            await users.DeleteAsync(user);
            return Redirect("/?auth=google-error#cuenta");
        }
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signIn.SignInAsync(user, isPersistent: false);
        return Redirect("/#cuenta");
    }
}
