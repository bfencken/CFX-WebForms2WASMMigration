using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs;
using CodeFactory.WinVs.Commands;
using CodeFactory.WinVs.Commands.VisualStudio;
using CodeFactory.WinVs.Models.ProjectSystem;
using WebForms2BlazorWASM.Dialog;
using WebForms2BlazorWASM.Migration;

namespace WebForms2BlazorWASM.Commands.Project
{
    /// <summary>
    /// Surfaces as "Setup Blazor WASM Projects" on right-click of a WebForms project.
    /// Runs the full migration sequence: scaffolds .Client/.Server/.Shared, migrates
    /// config, logic, static files, .aspx pages, API layer, and Program.cs.
    ///
    /// CFX Rule 1:  Both EnableCommandAsync and ExecuteCommandAsync implemented.
    /// CFX Rule 2:  EnableCommandAsync never throws — try/catch returns false.
    /// CFX Rule 3:  EnableCommandAsync evaluates real model (not always true).
    /// CFX Rule 6:  [CommandModel] attribute with title and description.
    /// CFX Rule 29: Idempotent — disabled once .Client project already exists.
    /// </summary>
    [CommandModel(
        commandTitle: "Setup Blazor WASM Projects",
        commandDescription: "Scaffolds .Client/.Server/.Shared and migrates the WingtipToys solution to Blazor WASM.")]
    public class SetupBlazorProject : ProjectCommandBase, IVsCommandInformation
    {
        /// <summary>
        /// Shows "Setup Blazor WASM Projects" only when:
        ///   • The source project exists (not null)
        ///   • The solution does NOT already contain a .Client project (idempotency)
        ///   • The source project contains at least one .aspx file (is a WebForms project)
        ///
        /// CFX Rule 2: entire body wrapped in try/catch — never throws.
        /// </summary>
        public override async Task<bool> EnableCommandAsync(VsProject project)
        {
            try
            {
                // CFX Rule 3: evaluate real model — not always true
                if (project == null) return false;

                var solution = await VisualStudioActions.SolutionActions.GetSolutionAsync();
                if (solution == null) return false;

                var projects = await solution.GetProjectsAsync(false);

                // CFX Rule 29: idempotency — hide if migration already ran
                if (projects.Any(p => p.Name.EndsWith(".Client", StringComparison.OrdinalIgnoreCase)))
                    return false;

                // Must be a WebForms project — check for at least one .aspx file
                var children = await project.GetChildrenAsync(true, false);
                var hasAspx = children
                    .OfType<VsDocument>()
                    .Any(d => d.Name.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                           && !d.Name.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase));

                return hasAspx;
            }
            catch
            {
                // CFX Rule 2: never throw from Enable
                return false;
            }
        }

        /// <summary>
        /// Shows SetupBlazorDialog to collect project names, then runs the full migration sequence.
        /// Logs each step to the CFX output window.
        ///
        /// CFX Rule 4:  Data model not cached — used immediately and discarded.
        /// CFX Rule 12: All VS Actions calls are awaited.
        /// CFX Rule 14: Output goes to VS output window, not Console/Debug.
        /// </summary>
        public override async Task ExecuteCommandAsync(VsProject project)
        {
            var output = VisualStudioActions.Environment.UserInterfaceActions.OutputWindowActions;

            try
            {
                // Show dialog to collect target project names
                var dialog = new SetupBlazorDialog();
                await VisualStudioActions.UserInterfaceActions.ShowDialogWindowAsync(dialog);

                // User cancelled — dialog raises Close without setting Result
                if (dialog.Result == null)
                {
                    await output.WriteToOutputWindowAsync("CFX WebForms2BlazorWASM: Migration cancelled by user.");
                    return;
                }

                // Build context from invocation-time data model (CFX Rule 4: not cached)
                var ctx = new MigrationContext
                {
                    SourceProject        = project,
                    ClientProjectName    = dialog.Result.ClientProjectName,
                    ServerProjectName    = dialog.Result.ServerProjectName,
                    SharedProjectName    = dialog.Result.SharedProjectName,
                    VisualStudioActions  = VisualStudioActions
                };

                // Status callback writes to CFX output window (CFX Rule 14)
                Action<string> status = async msg =>
                    await output.WriteToOutputWindowAsync($"CFX: {msg}");

                await output.WriteToOutputWindowAsync("CFX WebForms2BlazorWASM: Starting full migration...");

                var engine = new MigrationEngine();
                await engine.RunFullMigration(ctx, status);

                await output.WriteToOutputWindowAsync("CFX WebForms2BlazorWASM: Migration complete.");
            }
            catch (Exception ex)
            {
                await output.WriteToOutputWindowAsync(
                    $"CFX WebForms2BlazorWASM: ERROR during migration — {ex.Message}");
            }
        }
    }
}
