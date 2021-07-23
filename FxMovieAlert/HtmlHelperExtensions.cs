using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FxMovieAlert.Utilities
{
    public static class HtmlHelperExtensions
    {
        public static string IsSelected(this IHtmlHelper htmlHelper, string page, string cssClass = "selected")
        {
            string currentPage = htmlHelper.ViewContext.RouteData.Values["page"] as string;

            return page.Equals(currentPage, StringComparison.InvariantCultureIgnoreCase) ?
                cssClass : String.Empty;
        }
    }
}