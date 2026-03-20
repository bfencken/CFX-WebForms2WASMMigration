using System.Windows.Controls;
using CodeFactory.WinVs.Commands.VisualStudio;

namespace WebForms2BlazorWASM.Dialog
{
    /// <summary>
    /// Progress display dialog for SetupBlazorProject command.
    /// Collects three project names and surfaces Run / Cancel.
    ///
    /// CFX Rule 23: Raises the CodeFactory Close event when workflow completes.
    /// CFX Rule 24: Invoked via VisualStudioActions.ShowDialogWindowAsync — never instantiated directly.
    /// </summary>
    public partial class SetupBlazorDialog : UserControl
    {
        /// <summary>
        /// Populated when the user clicks Run. Null if the user cancelled.
        /// </summary>
        public SetupBlazorResult Result { get; private set; }

        /// <summary>
        /// Raised by CodeFactory to dismiss the dialog window.
        /// Must be raised when workflow completes — either Run or Cancel.
        /// CFX Rule 23: failing to raise this leaves the dialog open and blocks VS.
        /// </summary>
        public event CloseDialogEventHandler Close;

        public SetupBlazorDialog()
        {
            InitializeComponent();
        }

        private void Run_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Result = new SetupBlazorResult
            {
                ClientProjectName = txtClientName.Text.Trim(),
                ServerProjectName = txtServerName.Text.Trim(),
                SharedProjectName = txtSharedName.Text.Trim()
            };

            // CFX Rule 23: must raise Close before migration begins
            Close?.Invoke(this, new CloseDialogEventArgs());
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Result = null;

            // CFX Rule 23: always raise Close — even on cancel
            Close?.Invoke(this, new CloseDialogEventArgs());
        }
    }

    /// <summary>POCO carrying the dialog's collected values to ExecuteCommandAsync.</summary>
    public class SetupBlazorResult
    {
        public string ClientProjectName { get; set; }
        public string ServerProjectName { get; set; }
        public string SharedProjectName { get; set; }
    }
}
