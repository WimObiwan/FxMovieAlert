using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FxMovies.Site.Pages;

public class AboutModel : PageModel
{
    public string Message { get; set; }

    public void OnGet()
    {
    }
}