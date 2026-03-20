using CodeFactory.WinVs.Models.ProjectSystem;
using CodeFactory.WinVs;

namespace WebForms2BlazorWASM.Migration
{
    /// <summary>
    /// Data carrier passed to all migration engine methods.
    /// No async operations, no VS API calls — pure data.
    /// Populated once at command invocation time; never cached across separate executions.
    /// CFX Rule 4: Do not cache data models between calls.
    /// </summary>
    public class MigrationContext
    {
        /// <summary>The source WebForms project being migrated.</summary>
        public VsProject SourceProject { get; set; }

        /// <summary>Target Blazor WASM client project name. Default: "WingtipToys.Client"</summary>
        public string ClientProjectName { get; set; }

        /// <summary>Target ASP.NET Core server/host project name. Default: "WingtipToys.Server"</summary>
        public string ServerProjectName { get; set; }

        /// <summary>Target shared class library project name. Default: "WingtipToys.Shared"</summary>
        public string SharedProjectName { get; set; }

        // Resolved after ScaffoldTargetProjects runs:

        /// <summary>Resolved .Client project reference. Null until ScaffoldTargetProjects completes.</summary>
        public VsProject ClientProject { get; set; }

        /// <summary>Resolved .Server project reference. Null until ScaffoldTargetProjects completes.</summary>
        public VsProject ServerProject { get; set; }

        /// <summary>Resolved .Shared project reference. Null until ScaffoldTargetProjects completes.</summary>
        public VsProject SharedProject { get; set; }

        /// <summary>VS Actions API — scoped from the calling command at invocation time.</summary>
        public IVsActions VisualStudioActions { get; set; }
    }
}
