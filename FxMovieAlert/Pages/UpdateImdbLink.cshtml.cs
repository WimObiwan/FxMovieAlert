using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.Core.Commands;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace FxMovieAlert.Pages;

public class UpdateImdbLinkModel : PageModel
{
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ImdbDbContext imdbDbContext;
    private readonly ILogger<UpdateImdbLinkModel> logger;
    private readonly IMovieCreationHelper movieCreationHelper;
    private readonly IUpdateImdbLinkCommand updateImdbLinkCommand;

    public UpdateImdbLinkModel(
        ILogger<UpdateImdbLinkModel> logger,
        FxMoviesDbContext fxMoviesDbContext,
        ImdbDbContext imdbDbContext,
        IMovieCreationHelper movieCreationHelper,
        IUpdateImdbLinkCommand updateImdbLinkCommand)
    {
        this.logger = logger;
        this.fxMoviesDbContext = fxMoviesDbContext;
        this.imdbDbContext = imdbDbContext;
        this.movieCreationHelper = movieCreationHelper;
        this.updateImdbLinkCommand = updateImdbLinkCommand;
    }

    public IActionResult OnGet()
    {
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostAsync(int? movieeventid, string setimdbid, string returnPage)
    {
        var editImdbLinks = ClaimChecker.Has(User.Identity, Claims.EditImdbLinks);

        if (editImdbLinks && movieeventid.HasValue && !string.IsNullOrEmpty(setimdbid))
        {
            var overwrite = false;
            var setIgnore = false;
            var match = Regex.Match(setimdbid, @"(tt\d+)");
            if (match.Success)
            {
                setimdbid = match.Groups[0].Value;
                overwrite = true;
            }
            else if (setimdbid.Equals("ignore", StringComparison.InvariantCultureIgnoreCase))
            {
                setimdbid = null;
                overwrite = true;
                setIgnore = true;
            }
            else if (setimdbid.Equals("remove", StringComparison.InvariantCultureIgnoreCase))
            {
                setimdbid = null;
                overwrite = true;
            }

            if (overwrite) await updateImdbLinkCommand.Execute(movieeventid.Value, setimdbid, setIgnore);
        }

        return Redirect(returnPage);
    }
}