using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ITextValuationService _valuationService;

    public IndexModel(
        ILogger<IndexModel> logger,
        ITextValuationService valuationService)
    {
        _logger = logger;
        _valuationService = valuationService;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }

        try
        {
            _logger.LogDebug("Evaluating text: {Text}", text);
            string id = await _valuationService.EvaluateAsync(text);
            return RedirectToPage("Summary", new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating text");
            ModelState.AddModelError(string.Empty, "Произошла ошибка при обработке текста");
            return Page();
        }
    }
}