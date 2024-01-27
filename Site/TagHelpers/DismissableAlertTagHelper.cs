using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace FxMovies.TagHelpers;

[HtmlTargetElement("dismissable-alert")]  
public class DismissableAlertTagHelper : TagHelper
{
    public string Id { get; set; }
    public int Expiration { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        string localStorageName = $"dismissable-alert-{Id}";
        output.TagName = "div";
        output.Attributes.SetAttribute("id", Id);
        output.AddClass("alert", HtmlEncoder.Default);
        output.AddClass("alert-success", HtmlEncoder.Default);
        output.AddClass("alert-dismissible", HtmlEncoder.Default);
        output.AddClass("fade", HtmlEncoder.Default);
        output.AddClass("in", HtmlEncoder.Default);
        output.AddClass("hide", HtmlEncoder.Default);

        output.Content.AppendHtml("""
<button type="button" class="close" data-dismiss="alert" aria-label="Close"><span aria-hidden="true">&times;</span></button>
""");

        output.Content.AppendHtml(await output.GetChildContentAsync());
        output.Content.AppendHtml($$"""
<script>
    window.addEventListener('load', function () {
    	const now = new Date()
        const store = localStorage.getItem("{{localStorageName}}")
        if (store === null || Date.parse(store) < now.getTime() - {{Expiration}} * 1000) {
            $("#{{Id}}").removeClass("hide");
        }
        $("#{{Id}} .close").on("click",function() {
            localStorage.setItem("{{localStorageName}}", now.toISOString());
        });
    })
</script>
""");
    }
}