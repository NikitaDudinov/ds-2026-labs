using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using Valuator.Services;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ITextValuationService _valuationService;

    public SummaryModel(ITextValuationService valuationService)
    {
        _valuationService = valuationService;
    }

    public string TextId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double? Rank { get; set; }
    public double Similarity { get; set; }
    public string CentrifugoToken { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var result = await _valuationService.GetResultAsync(id);
        if (result == null) return NotFound();

        TextId = id;
        Text = result.Text;
        Rank = result.Rank;
        Similarity = result.Similarity;

        if (!Rank.HasValue)
        {
            CentrifugoToken = GenerateCentrifugoToken(id);
        }

        return Page();
    }

    private string GenerateCentrifugoToken(string userId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("super_secret_hmac_key_for_valuator_app_12345"));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}