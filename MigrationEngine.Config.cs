using System;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WebForms2BlazorWASM.Migration
{
    public partial class MigrationEngine
    {
        /// <summary>
        /// Reads web.config from the source project, extracts the 'WingtipToys' connection string,
        /// and writes appsettings.json to the Server project root.
        ///
        /// CFX Rule 30 (zero-token layer): config routing is structurally encoded here —
        ///   connection strings always go to .Server, NEVER to .Client.
        ///   This decision must not be left to runtime reasoning.
        /// </summary>
        public async Task MigrateConfig(MigrationContext ctx, Action<string> status)
        {
            var sourceChildren = await ctx.SourceProject.GetChildrenAsync(true, false);

            VsDocument webConfig = null;
            foreach (var child in sourceChildren)
            {
                if (child is VsDocument doc &&
                    doc.Name.Equals("web.config", StringComparison.OrdinalIgnoreCase))
                {
                    webConfig = doc;
                    break;
                }
            }

            if (webConfig == null)
            {
                WriteStatus(status, "  WARNING: web.config not found — appsettings.json not generated.");
                return;
            }

            var xmlContent = await webConfig.GetDocumentContentAsStringAsync();
            var connectionString = ExtractConnectionString(xmlContent, "WingtipToys");

            if (connectionString == null)
            {
                WriteStatus(status, "  WARNING: Connection string 'WingtipToys' not found in web.config.");
                connectionString = "Server=(localdb)\\\\mssqllocaldb;Database=WingtipToys;Trusted_Connection=True;";
            }

            // CFX Rule 30: config routing is zero-token layer.
            // Connection strings are server-side only. Never write to .Client.
            var appSettings = BuildAppSettings(connectionString);

            await ctx.ServerProject.AddDocumentAsync("appsettings.json", appSettings);
            WriteStatus(status, $"  appsettings.json written to {ctx.ServerProjectName} with connection string 'WingtipToys'.");
        }

        // ── SourceFormatter: appsettings.json Builder ────────────────────────
        // CFX Rule 30: appsettings.json structure is fixed — encoded permanently.
        // Logging and AllowedHosts sections included for .NET 8 compatibility.

        private static string BuildAppSettings(string connectionString)
        {
            var formatter = new CodeFactory.SourceFormatter();
            formatter.AppendCodeLine(0, "{");
            formatter.AppendCodeLine(1, "\"ConnectionStrings\": {");
            formatter.AppendCodeLine(2, $"\"WingtipToys\": \"{connectionString}\"");
            formatter.AppendCodeLine(1, "},");
            formatter.AppendCodeLine(1, "\"Logging\": {");
            formatter.AppendCodeLine(2, "\"LogLevel\": {");
            formatter.AppendCodeLine(3, "\"Default\": \"Information\",");
            formatter.AppendCodeLine(3, "\"Microsoft.AspNetCore\": \"Warning\"");
            formatter.AppendCodeLine(2, "}");
            formatter.AppendCodeLine(1, "},");
            formatter.AppendCodeLine(1, "\"AllowedHosts\": \"*\"");
            formatter.AppendCodeLine(0, "}");
            return formatter.ReturnSource();
        }

        private static string ExtractConnectionString(string webConfigXml, string name)
        {
            try
            {
                var doc = XDocument.Parse(webConfigXml);
                var connStrings = doc.Root?.Element("connectionStrings");
                if (connStrings == null) return null;

                foreach (var add in connStrings.Elements("add"))
                {
                    var attrName = add.Attribute("name")?.Value;
                    if (string.Equals(attrName, name, StringComparison.OrdinalIgnoreCase))
                        return add.Attribute("connectionString")?.Value;
                }
            }
            catch
            {
                // Malformed XML — caller handles null return
            }

            return null;
        }
    }
}
