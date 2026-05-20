using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;
using Microsoft.AspNetCore.Identity;

namespace Valuator.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IEvaluationStorage _storage;
    public RegisterModel(IEvaluationStorage storage) => _storage = storage;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string username, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ModelState.AddModelError("", "Заполните все поля");
            return Page();
        }

        if (password != confirmPassword)
        {
            ModelState.AddModelError("", "Пароли не совпадают");
            return Page();
        }

        if (await _storage.UserExistsAsync(username))
        {
            ModelState.AddModelError("", "Пользователь с таким логином уже существует");
            return Page();
        }

        var hasher = new PasswordHasher<string>();
        string hashedPassword = hasher.HashPassword(null!, password);
        await _storage.SaveUserAsync(username, hashedPassword);

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}