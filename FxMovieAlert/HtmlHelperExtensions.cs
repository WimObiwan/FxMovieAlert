using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FxMovieAlert.Utilities;

public static class HtmlHelperExtensions
{
    public static string IsSelected(this IHtmlHelper htmlHelper, string page, string cssClass = "selected")
    {
        var currentPage = htmlHelper.ViewContext.RouteData.Values["page"] as string;

        return page.Equals(currentPage, StringComparison.InvariantCultureIgnoreCase) ? cssClass : string.Empty;
    }
}