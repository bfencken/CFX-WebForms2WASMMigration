using System;
using System.Linq;
using System.Threading.Tasks;
using CodeFactory.WinVs;
using CodeFactory.WinVs.Commands;
using CodeFactory.WinVs.Commands.VisualStudio;
using CodeFactory.WinVs.Models.ProjectSystem;
using WebForms2BlazorWASM.Dialog;
using WebForms2BlazorWASM.Migration;

namespace WebForms2BlazorWASM.Commands.Document
{
    /// <summary>
    /// Surfaces as "Migrate WebForm to WASM" on right-click of any .aspx file.
    /// Migrates a single .aspx page to a Blazor .razor component in WingtipToys.Client/Pages/.
    ///
    /// CFX Rule 1:  Both EnableCommandAsync and ExecuteCommandAsync implemented.
    /// CFX Rule 2:  EnableCommandAsync never throws — try/catch returns false.
    /// CFX Rule 3:  EnableCommandAsync evaluates real model (not always true).
    /// CFX Rule 6:  [CommandModel] attribute with title and description.
    /// CFX Rule 29: Idempotent — disabled if .razor already exists in Pages/.
    /// </summary>
    [CommandModel(
        commandTitle: "Migrate WebForm to WASM",
        commandDescription: "Migrates a single .aspx page to a Blazor WASM .razor component in WingtipToys.Client.")]
    public class MigrateWebForm : DocumentCommandBase, IVsCommandInformation
    {
        /// <summary>
        /// Shows "Migrate WebForm to WASM" only when:
        ///   • The document is an .aspx file (not .aspx.cs code-behind)
        ///   • The solution contains a .Client project (SetupBlazorProject must run first)
        ///   • The corresponding .razor does NOT already exist in .Client/Pages/ (idempotency)
        ///
        /// CFX Rule 2: entire body wrapped in try/catch — never throws.
        /// </summary>
        public override async Task<bool> EnableCommandAsync(VsDocument document)
        {
            try
            {
                if (document == null) return false;

                // Must be a .aspx file — exclude .aspx.cs code-behind files
                if (!document.Name.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (document.Name.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase))
                    return false;

                var solution = await VisualStudioActions.SolutionActions.GetSolutionAsync();
                if (solution == null) return false;

                var projects = await solution.GetProjectsAsync(false);

                // SetupBlazorProject must have run first — .Client must exist
                var clientProject = projects.FirstOrDefault(p =>
                    p.Name.EndsWith(".Client", StringComparison.OrdinalIgnoreCase));

                if (clientProject == null) return false;

                // CFX Rule 29: idempotency — hide if .razor already exists in Pages/
                var razorName = DeriveRazorName(document.Name);
                var clientChildren = await clientProject.GetChildrenAsync(true, false);

                var pagesFolder = clientChildren
                    .OfType<VsProjectFolder>()
                    .FirstOrDefault(f => f.Name.Equals("Pages", StringComparison.OrdinalIgnoreCase));

                if (pagesFolder != null)
                {
                    var pageChildren = await pagesFolder.GetChildrenAsync(false, false);
                    if (pageChildren.OfType<VsDocument>().Any(d =>
                        d.Name.Equals(razorName, StringComparison.OrdinalIgnoreCase)))
                        return false; // Already migrated
                }

                return true;
            }
            catch
            {
                // CFX Rule 2: never throw from Enable
                return false;
            }
        }

        /// <summary>
        /// Shows MigrateWebFormDialog (progress display), then migrates the .aspx to .razor.
        ///
        /// CFX Rule 4:  Data model not cached — resolved fresh at invocation.
        /// CFX Rule 12: All VS Actions calls are awaited.
        /// CFX Rule 14: Output goes to VS output window.
        /// </summary>
        public override async Task ExecuteCommandAsync(VsDocument document)
        {
            var output = VisualStudioActions.Environment.UserInterfaceActions.OutputWindowActions;

            try
            {
                var solution = await VisualStudioActions.SolutionActions.GetSolutionAsync();
                var projects = await solution.GetProjectsAsync(false);

                // Resolve .Client project at invocation time (CFX Rule 4: not cached)
                var clientProject = projects.FirstOrDefault(p =>
                    p.Name.EndsWith(".Client", StringComparison.OrdinalIgnoreCase));

                if (clientProject == null)
                {
                    await output.WriteToOutputWindowAsync(
                        "CFX MigrateWebForm: ERROR — .Client project not found. Run 'Setup Blazor WASM Projects' first.");
                    return;
                }

                var sourceProject = await document.GetParentAsync() as VsProject;

                // Build minimal context for single-page migration
                var ctx = new MigrationContext
                {
                    SourceProject       = sourceProject,
                    ClientProject       = clientProject,
                    ClientProjectName   = clientProject.Name,
                    SharedProjectName   = projects
                        .FirstOrDefault(p => p.Name.EndsWith(".Shared", StringComparison.OrdinalIgnoreCase))
                        ?.Name ?? "WingtipToys.Shared",
                    VisualStudioActions = VisualStudioActions
                };

                // Show progress dialog (display only — no input fields)
                var dialog = new MigrateWebFormDialog();

                Action<string> status = msg =>
                {
                    dialog.AddStatus(msg);
                    output.WriteToOutputWindowAsync($"CFX: {msg}");
                };

                // Show dialog non-blocking, then run migration
                _ = VisualStudioActions.UserInterfaceActions.ShowDialogWindowAsync(dialog);

                var engine = new MigrationEngine();
                await engine.MigrateSinglePage(document, ctx, status);

                dialog.SignalComplete();

                await output.WriteToOutputWindowAsync(
                    $"CFX MigrateWebForm: {document.Name} migrated successfully.");
            }
            catch (Exception ex)
            {
                await output.WriteToOutputWindowAsync(
                    $"CFX MigrateWebForm: ERROR — {ex.Message}");
            }
        }

        private static string DeriveRazorName(string aspxFileName)
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(aspxFileName);
            if (baseName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                baseName = "Index";
            return baseName + ".razor";
        }
    }
}
