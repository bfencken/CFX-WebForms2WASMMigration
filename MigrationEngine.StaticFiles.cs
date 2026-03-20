using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs.Models.ProjectSystem;

namespace WebForms2BlazorWASM.Migration
{
    public partial class MigrationEngine
    {
        /// <summary>
        /// Migrates static assets from the WebForms source project to the Blazor WASM client's wwwroot.
        ///
        /// Copies:
        ///   Catalog/Images/Thumbs/*.png → ClientProject/wwwroot/images/thumbs/
        ///   Content/Site.css            → ClientProject/wwwroot/css/site.css
        ///
        /// CFX Rule 29 (idempotency): skips files that already exist in wwwroot.
        /// CFX Rule 27 (output cleanliness): no CodeFactory references in generated content.
        /// </summary>
        public async Task MigrateStaticFiles(MigrationContext ctx, Action<string> status)
        {
            await MigrateProductImages(ctx, status);
            await MigrateSiteCss(ctx, status);
        }

        private async Task MigrateProductImages(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, true);

            // Navigate to Catalog/Images/Thumbs/
            var catalogFolder = sourceChildren
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals("Catalog", StringComparison.OrdinalIgnoreCase));

            if (catalogFolder == null)
            {
                WriteStatus(status, "  WARNING: Catalog/ folder not found — product images not migrated.");
                return;
            }

            var catalogChildren = await catalogFolder.GetChildrenAsync(true, true);

            var thumbsFolder = catalogChildren
                .OfType<VsProjectFolder>()
                .SelectMany(f => new[] { f }.Concat(
                    f.GetChildrenAsync(true, true).Result.OfType<VsProjectFolder>()))
                .FirstOrDefault(f => f.Name.Equals("Thumbs", StringComparison.OrdinalIgnoreCase));

            if (thumbsFolder == null)
            {
                WriteStatus(status, "  WARNING: Catalog/Images/Thumbs/ not found — product images not migrated.");
                return;
            }

            var thumbChildren = await thumbsFolder.GetChildrenAsync(false, false);
            var pngFiles = thumbChildren
                .OfType<VsDocument>()
                .Where(d => d.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();

            WriteStatus(status, $"  Found {pngFiles.Count} product thumbnail(s) to migrate.");

            // Ensure wwwroot/images/thumbs/ exists in .Client
            var wwwroot = await GetOrCreateFolder(ctx.ClientProject, "wwwroot");
            var imagesFolder = await GetOrCreateFolderInFolder(wwwroot, "images");
            var destThumbsFolder = await GetOrCreateFolderInFolder(imagesFolder, "thumbs");

            foreach (var png in pngFiles)
            {
                // CFX Rule 29: skip if already present
                var destChildren = await destThumbsFolder.GetChildrenAsync(false, false);
                if (destChildren.OfType<VsDocument>().Any(d => d.Name.Equals(png.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    WriteStatus(status, $"  SKIP {png.Name} — already in wwwroot/images/thumbs/");
                    continue;
                }

                await destThumbsFolder.AddExistingDocumentAsync(png.FilePath);
                WriteStatus(status, $"  → {png.Name} → wwwroot/images/thumbs/");
            }
        }

        private async Task MigrateSiteCss(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, true);

            var contentFolder = sourceChildren
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals("Content", StringComparison.OrdinalIgnoreCase));

            if (contentFolder == null)
            {
                WriteStatus(status, "  WARNING: Content/ folder not found — site.css not migrated.");
                return;
            }

            var contentChildren = await contentFolder.GetChildrenAsync(false, false);
            var siteCss = contentChildren
                .OfType<VsDocument>()
                .FirstOrDefault(d => d.Name.Equals("Site.css", StringComparison.OrdinalIgnoreCase));

            if (siteCss == null)
            {
                WriteStatus(status, "  WARNING: Content/Site.css not found.");
                return;
            }

            var wwwroot = await GetOrCreateFolder(ctx.ClientProject, "wwwroot");
            var cssFolder = await GetOrCreateFolderInFolder(wwwroot, "css");

            var destChildren = await cssFolder.GetChildrenAsync(false, false);
            if (destChildren.OfType<VsDocument>().Any(d => d.Name.Equals("site.css", StringComparison.OrdinalIgnoreCase)))
            {
                WriteStatus(status, "  SKIP site.css — already in wwwroot/css/");
                return;
            }

            var cssContent = await siteCss.GetDocumentContentAsStringAsync();
            await cssFolder.AddDocumentAsync("site.css", cssContent);
            WriteStatus(status, "  → site.css → wwwroot/css/");
        }

        // ── Helper: nested folder creation ───────────────────────────────────

        private static async Task<VsProjectFolder> GetOrCreateFolderInFolder(
            VsProjectFolder parent,
            string folderName)
        {
            var children = await parent.GetChildrenAsync(false, false);
            var existing = children
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

            if (existing != null) return existing;

            return await parent.AddProjectFolderAsync(folderName);
        }
    }
}
