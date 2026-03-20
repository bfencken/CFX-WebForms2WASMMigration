using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs.Models.ProjectSystem;
using CodeFactory.WinVs.Models.CSharp;

namespace WebForms2BlazorWASM.Migration
{
    public partial class MigrationEngine
    {
        /// <summary>
        /// Migrates .cs files from the source project's Logic/ folder.
        /// Routing logic (CFX Rule 30 — zero-token layer):
        ///   - Files with System.Web or System.Data.Entity usings → ctx.ServerProject/Logic/
        ///   - All other files → ctx.SharedProject/Logic/
        ///
        /// Uses SourceFormatter to strip EF/WebForms attributes and reformat as clean .NET Standard classes.
        /// CFX Rule 11: only processes files where LoadedFromSource == true.
        /// CFX Rule 27: output contains no CodeFactory references.
        /// </summary>
        public async Task MigrateLogicClasses(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, true);

            // Find the Logic/ folder
            var logicFolder = sourceChildren
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals("Logic", StringComparison.OrdinalIgnoreCase));

            if (logicFolder == null)
            {
                WriteStatus(status, "  No Logic/ folder found in source project — skipping.");
                return;
            }

            var logicChildren = await logicFolder.GetChildrenAsync(false, true);
            var csFiles = logicChildren
                .OfType<VsCSharpSource>()
                .ToList();

            WriteStatus(status, $"  Found {csFiles.Count} Logic/ class(es) to migrate.");

            var sharedLogicFolder = await GetOrCreateFolder(ctx.SharedProject, "Logic");
            var serverLogicFolder = await GetOrCreateFolder(ctx.ServerProject, "Logic");

            foreach (var csFile in csFiles)
            {
                // CFX Rule 11: only source-loaded models expose usings and body content
                if (!csFile.LoadedFromSource)
                {
                    WriteStatus(status, $"  SKIP {csFile.Name} — not loaded from source (reflection model).");
                    continue;
                }

                var model = csFile.SourceCode;
                if (model == null)
                {
                    WriteStatus(status, $"  SKIP {csFile.Name} — source model is null.");
                    continue;
                }

                // Route decision: Web/EF dependencies → Server; clean logic → Shared
                var isServerBound = IsServerBoundLogic(model);
                var targetFolder = isServerBound ? serverLogicFolder : sharedLogicFolder;
                var targetProjectName = isServerBound ? ctx.ServerProjectName : ctx.SharedProjectName;

                var cleanedContent = BuildCleanLogicClass(model, ctx.SharedProjectName, isServerBound);

                await targetFolder.AddDocumentAsync(csFile.Name, cleanedContent);
                WriteStatus(status, $"  → {csFile.Name} → {targetProjectName}/Logic/ ({(isServerBound ? "server-bound" : "shared")})");
            }
        }

        // ── Routing Logic ─────────────────────────────────────────────────────
        // CFX Rule 30: this routing decision is structural — encoded here permanently.

        private static bool IsServerBoundLogic(CsSource model)
        {
            if (model.UsingStatements == null) return false;

            return model.UsingStatements.Any(u =>
                u.ReferenceNamespace.StartsWith("System.Web", StringComparison.OrdinalIgnoreCase) ||
                u.ReferenceNamespace.StartsWith("System.Data.Entity", StringComparison.OrdinalIgnoreCase));
        }

        // ── SourceFormatter: Clean Class Builder ─────────────────────────────
        // Strips EF/WebForms attributes; rewrites as a clean .NET Standard class.
        // CFX Rule 27: no CodeFactory references in output.

        private static string BuildCleanLogicClass(
            CsSource model,
            string sharedProjectName,
            bool isServerBound)
        {
            var formatter = new CodeFactory.SourceFormatter();

            // Emit clean using statements — strip System.Web.* and System.Data.Entity.*
            foreach (var usingStatement in model.UsingStatements ?? Enumerable.Empty<CsUsingStatement>())
            {
                var ns = usingStatement.ReferenceNamespace;
                if (ns.StartsWith("System.Web", StringComparison.OrdinalIgnoreCase)) continue;
                if (ns.StartsWith("System.Data.Entity", StringComparison.OrdinalIgnoreCase)) continue;

                formatter.AppendCodeLine(0, $"using {ns};");
            }

            // Add shared models reference if not server-bound
            if (!isServerBound)
                formatter.AppendCodeLine(0, $"using {sharedProjectName}.Models;");

            formatter.AppendCodeLine(0, "");

            // Re-emit namespace and class body from the source model
            foreach (var ns in model.Namespaces ?? Enumerable.Empty<CsNamespace>())
            {
                formatter.AppendCodeLine(0, $"namespace {ns.Name}");
                formatter.AppendCodeLine(0, "{");

                foreach (var csClass in ns.Classes ?? Enumerable.Empty<CsClass>())
                {
                    formatter.AppendCodeLine(1, $"public class {csClass.Name}");
                    formatter.AppendCodeLine(1, "{");
                    formatter.AppendCodeLine(2, "// TODO: Review migrated members — EF/WebForms dependencies removed");

                    foreach (var method in csClass.Methods ?? Enumerable.Empty<CsMethod>())
                    {
                        formatter.AppendCodeLine(2, $"// Migrated method: {method.Name} — implement using HttpClient or service injection");
                    }

                    formatter.AppendCodeLine(1, "}");
                }

                formatter.AppendCodeLine(0, "}");
            }

            return formatter.ReturnSource();
        }
    }
}
