using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FxMovies.Site.Pages;

public class IndexModel : PageModel
{
    private static readonly HashSet<string> ValidPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Broadcasts",
        "/FreeStreaming",
        "/PaidStreaming"
    };

    public IActionResult OnGet()
    {
        // Check if there's a cookie with the last visited page
        if (Request.Cookies.TryGetValue(BroadcastsModelBase.LastVisitedPageCookieName, out var lastVisitedPage)
            && !string.IsNullOrEmpty(lastVisitedPage)
            && ValidPages.Contains(lastVisitedPage))
        {
            return RedirectToPage(lastVisitedPage);
        }

        return RedirectToPage("/Broadcasts");
    }
}