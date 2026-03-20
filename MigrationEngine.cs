using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs.Models.ProjectSystem;

namespace WebForms2BlazorWASM.Migration
{
    /// <summary>
    /// Partial class root — orchestrates the full WebForms → Blazor WASM migration sequence.
    /// Split across domain-specific partials:
    ///   MigrationEngine.AspxFiles.cs
    ///   MigrationEngine.Logic.cs
    ///   MigrationEngine.StaticFiles.cs
    ///   MigrationEngine.Config.cs
    ///   MigrationEngine.ApiLayer.cs
    /// </summary>
    public partial class MigrationEngine
    {
        /// <summary>
        /// Runs the full migration sequence in order.
        /// Called by SetupBlazorProject.ExecuteCommandAsync after dialog input is collected.
        /// Each step logs to the VS output window via the statusCallback.
        /// </summary>
        public async Task RunFullMigration(MigrationContext ctx, Action<string> status)
        {
            WriteStatus(status, "── [1/7] Scaffolding target projects (.Client / .Server / .Shared)...");
            await ScaffoldTargetProjects(ctx, status);

            WriteStatus(status, "── [2/7] Migrating web.config → appsettings.json...");
            await MigrateConfig(ctx, status);

            WriteStatus(status, "── [3/7] Migrating Logic/ classes...");
            await MigrateLogicClasses(ctx, status);

            WriteStatus(status, "── [4/7] Migrating static files (images, CSS)...");
            await MigrateStaticFiles(ctx, status);

            WriteStatus(status, "── [5/7] Migrating .aspx pages → .razor components...");
            await MigrateAspxFiles(ctx, status);

            WriteStatus(status, "── [6/7] Scaffolding API layer (controllers)...");
            await ScaffoldApiLayer(ctx, status);

            WriteStatus(status, "── [7/7] Generating WingtipToys.Client/Program.cs...");
            await ScaffoldClientProgram(ctx, status);
        }

        /// <summary>
        /// Scaffolds the three target projects (.Client, .Server, .Shared) into the solution.
        /// Resolves project references back onto ctx after creation.
        /// CFX Rule 10: Re-fetch models after mutations.
        /// </summary>
        public async Task ScaffoldTargetProjects(MigrationContext ctx, Action<string> status)
        {
            var vsActions = ctx.VisualStudioActions;
            var solution = await vsActions.SolutionActions.GetSolutionAsync();

            // Scaffold .Shared — .NET Standard 2.1 class library
            WriteStatus(status, $"  Creating {ctx.SharedProjectName} (.NET Standard 2.1)...");
            await vsActions.ProjectActions.AddProjectAsync(
                ctx.SharedProjectName,
                "classlib",
                solution.Path,
                new[] { "--framework", "netstandard2.1" });

            // Scaffold .Server — ASP.NET Core Web API, .NET 8
            WriteStatus(status, $"  Creating {ctx.ServerProjectName} (ASP.NET Core Web API, .NET 8)...");
            await vsActions.ProjectActions.AddProjectAsync(
                ctx.ServerProjectName,
                "webapi",
                solution.Path,
                new[] { "--framework", "net8.0" });

            // Scaffold .Client — Blazor WASM, .NET 8
            WriteStatus(status, $"  Creating {ctx.ClientProjectName} (Blazor WASM, .NET 8)...");
            await vsActions.ProjectActions.AddProjectAsync(
                ctx.ClientProjectName,
                "blazorwasm",
                solution.Path,
                new[] { "--framework", "net8.0" });

            // Re-fetch solution to resolve project references (CFX Rule 10)
            solution = await vsActions.SolutionActions.GetSolutionAsync();
            var projects = await solution.GetProjectsAsync(false);

            ctx.SharedProject = projects.FirstOrDefault(p =>
                p.Name.Equals(ctx.SharedProjectName, StringComparison.OrdinalIgnoreCase));
            ctx.ServerProject = projects.FirstOrDefault(p =>
                p.Name.Equals(ctx.ServerProjectName, StringComparison.OrdinalIgnoreCase));
            ctx.ClientProject = projects.FirstOrDefault(p =>
                p.Name.Equals(ctx.ClientProjectName, StringComparison.OrdinalIgnoreCase));

            WriteStatus(status, "  Target projects scaffolded and resolved.");
        }

        /// <summary>
        /// Generates and writes WingtipToys.Client/Program.cs.
        /// Content is structurally fixed — encoded here permanently per CFX Rule 30 (zero-token layer).
        /// </summary>
        public async Task ScaffoldClientProgram(MigrationContext ctx, Action<string> status)
        {
            var clientProjectName = ctx.ClientProjectName;

            // CFX Rule 30: structural Program.cs content encoded permanently in SourceFormatter.
            // Route topology, DI registration, and WASM bootstrap pattern are zero-token layer.
            var formatter = new CodeFactory.SourceFormatter();
            formatter.AppendCodeLine(0, $"using Microsoft.AspNetCore.Components.Web;");
            formatter.AppendCodeLine(0, $"using Microsoft.AspNetCore.Components.WebAssembly.Hosting;");
            formatter.AppendCodeLine(0, $"using {clientProjectName};");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, "var builder = WebAssemblyHostBuilder.CreateDefault(args);");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, "builder.RootComponents.Add<App>(\"#app\");");
            formatter.AppendCodeLine(0, "builder.RootComponents.Add<HeadOutlet>(\"head::after\");");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, "builder.Services.AddScoped(sp =>");
            formatter.AppendCodeLine(1, "new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });");
            formatter.AppendCodeLine(0, "");
            formatter.AppendCodeLine(0, "await builder.Build().RunAsync();");

            var programContent = formatter.ReturnSource();

            var folder = await GetOrCreateFolder(ctx.ClientProject, null);
            await ctx.ClientProject.AddDocumentAsync("Program.cs", programContent);

            WriteStatus(status, $"  {clientProjectName}/Program.cs written.");
        }

        // ── Shared Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Finds a project in the solution by name suffix match.
        /// CFX Rule 9: returns null if not found — callers must null-check.
        /// </summary>
        protected async Task<VsProject> FindProjectByNameSuffix(MigrationContext ctx, string suffix)
        {
            var solution = await ctx.VisualStudioActions.SolutionActions.GetSolutionAsync();
            var projects = await solution.GetProjectsAsync(false);
            return projects.FirstOrDefault(p =>
                p.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets an existing folder in a project by name, or creates it if absent.
        /// Pass null for folderName to return the project root.
        /// </summary>
        protected async Task<VsProjectFolder> GetOrCreateFolder(VsProject project, string folderName)
        {
            if (folderName == null) return null;

            var children = await project.GetChildrenAsync(false, false);
            var existing = children
                .OfType<VsProjectFolder>()
                .FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

            if (existing != null) return existing;

            return await project.AddProjectFolderAsync(folderName);
        }

        /// <summary>
        /// Writes a status message to both the VS output window (via callback) and the action log.
        /// CFX Rule 14: never use Console.WriteLine — use the output callback only.
        /// </summary>
        protected static void WriteStatus(Action<string> status, string message)
        {
            status?.Invoke(message);
        }
    }
}
