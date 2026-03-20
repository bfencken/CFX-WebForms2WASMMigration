using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs.Models.ProjectSystem;

namespace WebForms2BlazorWASM.Migration
{
    public partial class MigrationEngine
    {
        /// <summary>
        /// Migrates all .aspx pages in the source project to Blazor .razor components
        /// in ctx.ClientProject/Pages/.
        ///
        /// CFX Rule 29 (idempotency): skips any page whose .razor equivalent already exists.
        /// CFX Rule 27 (output cleanliness): generated .razor files contain zero CodeFactory references.
        /// CFX Rule 30 (zero-token layer): route format, injection pattern, lifecycle method,
        ///   and async discipline are encoded here permanently — never left to runtime reasoning.
        /// </summary>
        public async Task MigrateAspxFiles(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, true);

            var aspxFiles = sourceChildren
                .OfType<VsDocument>()
                .Where(d => d.Name.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                         && !d.Name.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            WriteStatus(status, $"  Found {aspxFiles.Count} .aspx page(s) to migrate.");

            var pagesFolder = await GetOrCreateFolder(ctx.ClientProject, "Pages");

            foreach (var aspxDoc in aspxFiles)
            {
                var razorName = DeriveRazorName(aspxDoc.Name);
                var componentName = System.IO.Path.GetFileNameWithoutExtension(razorName);
                var route = DeriveRoute(componentName);

                // CFX Rule 29: skip if .razor already exists (idempotent)
                var existingChildren = await pagesFolder.GetChildrenAsync(false, false);
                var alreadyExists = existingChildren
                    .OfType<VsDocument>()
                    .Any(d => d.Name.Equals(razorName, StringComparison.OrdinalIgnoreCase));

                if (alreadyExists)
                {
                    WriteStatus(status, $"  SKIP {razorName} — already exists.");
                    continue;
                }

                var content = BuildRazorPage(route, ctx.SharedProjectName, componentName, aspxDoc.Name);

                await pagesFolder.AddDocumentAsync(razorName, content);
                WriteStatus(status, $"  → {razorName} created in Pages/");
            }
        }

        /// <summary>
        /// Migrates a single .aspx document to a .razor component.
        /// Called by MigrateWebForm command for on-demand per-page migration.
        /// CFX Rule 29: skips if target already exists.
        /// </summary>
        public async Task MigrateSinglePage(
            VsDocument aspxDocument,
            MigrationContext ctx,
            Action<string> status)
        {
            var razorName = DeriveRazorName(aspxDocument.Name);
            var componentName = System.IO.Path.GetFileNameWithoutExtension(razorName);
            var route = DeriveRoute(componentName);

            var pagesFolder = await GetOrCreateFolder(ctx.ClientProject, "Pages");

            var existingChildren = await pagesFolder.GetChildrenAsync(false, false);
            var alreadyExists = existingChildren
                .OfType<VsDocument>()
                .Any(d => d.Name.Equals(razorName, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                WriteStatus(status, $"  SKIP {razorName} — already exists (idempotency).");
                return;
            }

            var content = BuildRazorPage(route, ctx.SharedProjectName, componentName, aspxDocument.Name);

            await pagesFolder.AddDocumentAsync(razorName, content);
            WriteStatus(status, $"  → {razorName} created in {ctx.ClientProjectName}/Pages/");
        }

        // ── SourceFormatter: Razor Page Builder ──────────────────────────────
        //
        // CFX Rule 30 — ZERO-TOKEN LAYER:
        // The following structural decisions are encoded permanently here.
        // They must never be left to runtime reasoning or SLM prompting:
        //   • @page route format
        //   • @using shared models namespace
        //   • @inject HttpClient Http pattern
        //   • OnInitializedAsync lifecycle method (never sync HTTP, never void)
        //   • TODO comment placement for business logic slots
        //
        // CFX Rule 27 — OUTPUT CLEANLINESS:
        // No "using CodeFactory.*" may appear in the generated content.
        // Verified: this method emits zero CodeFactory references.

        private static string BuildRazorPage(
            string route,
            string sharedProjectName,
            string componentName,
            string sourceFileName)
        {
            var formatter = new CodeFactory.SourceFormatter();

            // Directives — zero-token structural layer
            formatter.AppendCodeLine(0, $"@page \"{route}\"");
            formatter.AppendCodeLine(0, $"@using {sharedProjectName}.Models");
            formatter.AppendCodeLine(0, "@inject HttpClient Http");
            formatter.AppendCodeLine(0, "");

            // Page metadata
            formatter.AppendCodeLine(0, $"<PageTitle>{componentName}</PageTitle>");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, $"<h3>{componentName}</h3>");
            formatter.AppendCodeLine(0, "");

            // Migration provenance comment
            formatter.AppendCodeLine(0, $"<!-- Migrated from: {sourceFileName} -->");
            formatter.AppendCodeLine(0, "<!-- TODO: Implement component body using the API calls in @code below -->");
            formatter.AppendCodeLine(0, "");

            // @code block — structural skeleton encoded, business logic slotted as TODO
            formatter.AppendCodeLine(0, "@code {");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(1, "private List<Product> _items;");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(1, "protected override async Task OnInitializedAsync()");
            formatter.AppendCodeLine(1, "{");
            formatter.AppendCodeLine(2, "// TODO: Replace with correct API endpoint for this page");
            formatter.AppendCodeLine(2, "_items = await Http.GetFromJsonAsync<List<Product>>(\"api/products\");");
            formatter.AppendCodeLine(1, "}");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, "}");

            return formatter.ReturnSource();
        }

        // ── Route Derivation ─────────────────────────────────────────────────
        // CFX Rule 30: route topology is structural — encoded here, not computed at runtime.

        private static string DeriveRazorName(string aspxFileName)
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(aspxFileName); // e.g. "Default"
            if (baseName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                baseName = "Index";
            return baseName + ".razor";
        }

        private static string DeriveRoute(string componentName)
        {
            return componentName switch
            {
                "Index"             => "/",
                "ProductList"       => "/products",
                "ProductDetails"    => "/product/{id:int}",
                "AddToCart"         => "/addtocart",
                "ShoppingCart"      => "/cart",
                "Checkout"          => "/checkout",
                "OrderConfirmation" => "/confirmation",
                _                   => "/" + componentName.ToLowerInvariant()
            };
        }
    }
}
