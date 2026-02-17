using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ITextValuationService _valuationService;

    public SummaryModel(ITextValuationService valuationService)
    {
        _valuationService = valuationService;
    }

    public string Text { get; set; } = string.Empty;
    public double Rank { get; set; }
    public double Similarity { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var result = await _valuationService.GetResultAsync(id);
        if (result == null) return NotFound();

        Text = result.Text;
        Rank = result.Rank;
        Similarity = result.Similarity;

        return Page();
    }
}