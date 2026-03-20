using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs.Models.ProjectSystem;
using CodeFactory.WinVs.Models.CSharp;

namespace WebForms2BlazorWASM.Migration
{
    public partial class MigrationEngine
    {
        // Models that get top-level controllers — per build spec Section 3.6
        // CartItem and OrderDetail are accessed via parent resources; no standalone controllers.
        private static readonly HashSet<string> ControllerTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Category", "Product", "Order"
        };

        // Pluralization map — deterministic, zero-token layer (CFX Rule 30)
        private static readonly Dictionary<string, string> PluralNames = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            { "Category", "Categories" },
            { "Product",  "Products"   },
            { "Order",    "Orders"     }
        };

        /// <summary>
        /// Scaffolds minimal API controllers in ctx.ServerProject/Controllers/ for each
        /// supported model class found in the source project's Models/ folder.
        ///
        /// Each controller exposes:
        ///   GET /api/{plural}        → GetAll()
        ///   GET /api/{plural}/{id}   → GetById(int id)
        ///
        /// CFX Rule 30 (zero-token layer): controller structure, route pattern, and
        ///   attribute placement are encoded in BuildApiController() permanently.
        /// CFX Rule 27 (output cleanliness): generated controllers contain no CodeFactory references.
        /// CFX Rule 29 (idempotency): skips controllers that already exist.
        /// </summary>
        public async Task ScaffoldApiLayer(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, true);

            var modelsFolder = sourceChildren
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals("Models", StringComparison.OrdinalIgnoreCase));

            if (modelsFolder == null)
            {
                WriteStatus(status, "  WARNING: Models/ folder not found in source — API layer not scaffolded.");
                return;
            }

            var modelChildren = await modelsFolder.GetChildrenAsync(false, true);
            var modelClasses = modelChildren
                .OfType<VsCSharpSource>()
                .Where(f => ControllerTargets.Contains(
                    System.IO.Path.GetFileNameWithoutExtension(f.Name)))
                .ToList();

            WriteStatus(status, $"  Found {modelClasses.Count} model class(es) to scaffold controllers for.");

            var controllersFolder = await GetOrCreateFolder(ctx.ServerProject, "Controllers");

            foreach (var modelFile in modelClasses)
            {
                var modelName = System.IO.Path.GetFileNameWithoutExtension(modelFile.Name);
                var pluralName = PluralNames.TryGetValue(modelName, out var plural) ? plural : modelName + "s";
                var controllerFileName = $"{pluralName}Controller.cs";

                // CFX Rule 29: skip if controller already exists
                var existingChildren = await controllersFolder.GetChildrenAsync(false, false);
                if (existingChildren.OfType<VsDocument>().Any(d =>
                    d.Name.Equals(controllerFileName, StringComparison.OrdinalIgnoreCase)))
                {
                    WriteStatus(status, $"  SKIP {controllerFileName} — already exists.");
                    continue;
                }

                var dbContextName = "WingtipToysContext";
                var content = BuildApiController(
                    pluralName,
                    modelName,
                    dbContextName,
                    ctx.SharedProjectName,
                    ctx.ServerProjectName);

                await controllersFolder.AddDocumentAsync(controllerFileName, content);
                WriteStatus(status, $"  → {controllerFileName} → {ctx.ServerProjectName}/Controllers/");
            }
        }

        // ── SourceFormatter: API Controller Builder ───────────────────────────
        //
        // CFX Rule 30 — ZERO-TOKEN LAYER:
        // The following are encoded permanently and must never be left to runtime reasoning:
        //   • [ApiController] + [Route("api/[controller]")] attribute pattern
        //   • Constructor injection of DbContext
        //   • GetAll() → ToListAsync() pattern
        //   • GetById() → FindAsync() with null-check returning NotFound()
        //   • Async/await throughout — no sync EF calls
        //
        // CFX Rule 27 — OUTPUT CLEANLINESS:
        // No "using CodeFactory.*" in output. Verified: zero CodeFactory references emitted.

        private static string BuildApiController(
            string pluralName,
            string modelName,
            string dbContextName,
            string sharedProjectName,
            string serverProjectName)
        {
            var formatter = new CodeFactory.SourceFormatter();

            formatter.AppendCodeLine(0, "using Microsoft.AspNetCore.Mvc;");
            formatter.AppendCodeLine(0, "using Microsoft.EntityFrameworkCore;");
            formatter.AppendCodeLine(0, $"using {sharedProjectName}.Models;");
            formatter.AppendCodeLine(0, $"using {serverProjectName}.Data;");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, $"namespace {serverProjectName}.Controllers");
            formatter.AppendCodeLine(0, "{");
            formatter.AppendCodeLine(1, "[ApiController]");
            formatter.AppendCodeLine(1, "[Route(\"api/[controller]\")]");
            formatter.AppendCodeLine(1, $"public class {pluralName}Controller : ControllerBase");
            formatter.AppendCodeLine(1, "{");
            formatter.AppendCodeLine(2, $"private readonly {dbContextName} _db;");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(2, $"public {pluralName}Controller({dbContextName} db) => _db = db;");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(2, "[HttpGet]");
            formatter.AppendCodeLine(2, $"public async Task<IActionResult> GetAll()");
            formatter.AppendCodeLine(3, $"=> Ok(await _db.{pluralName}.ToListAsync());");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(2, "[HttpGet(\"{id:int}\")]");
            formatter.AppendCodeLine(2, $"public async Task<IActionResult> GetById(int id)");
            formatter.AppendCodeLine(2, "{");
            formatter.AppendCodeLine(3, $"var item = await _db.{pluralName}.FindAsync(id);");
            formatter.AppendCodeLine(3, "return item is null ? NotFound() : Ok(item);");
            formatter.AppendCodeLine(2, "}");
            formatter.AppendCodeLine(1, "}");
            formatter.AppendCodeLine(0, "}");

            return formatter.ReturnSource();
        }
    }
}
