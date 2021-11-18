using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FxMovieAlert.Pages;

public class IndexModel : PageModel
{
    public IndexModel()
    {
    }

    public IActionResult OnGet()
    {
        return RedirectToPage("/Broadcasts");
    }
}
