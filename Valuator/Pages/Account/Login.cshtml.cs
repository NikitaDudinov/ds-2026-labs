using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;
using Microsoft.AspNetCore.Identity;

namespace Valuator.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IEvaluationStorage _storage;
    public LoginModel(IEvaluationStorage storage) => _storage = storage;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Заполните все поля");
            return Page();
        }

        var savedPasswordHash = await _storage.GetUserPasswordAsync(username);

        if (savedPasswordHash == null)
        {
            ModelState.AddModelError("", "Неверный логин или пароль");
            return Page();
        }

        var hasher = new PasswordHasher<string>();
        var verificationResult = hasher.VerifyHashedPassword(null!, savedPasswordHash, password);

        if (verificationResult != PasswordVerificationResult.Success)
        {
            ModelState.AddModelError("", "Неверный логин или пароль");
            return Page();
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}