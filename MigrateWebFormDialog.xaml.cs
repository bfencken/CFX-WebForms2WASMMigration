using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeFactory.WinVs.Commands.VisualStudio;

namespace WebForms2BlazorWASM.Dialog
{
    /// <summary>
    /// Progress-only dialog for MigrateWebForm command.
    /// No user input — displays migration step messages as they arrive.
    /// The Close button is enabled only after SignalComplete() is called.
    ///
    /// CFX Rule 23: Raises the CodeFactory Close event when the user dismisses.
    /// CFX Rule 24: Invoked via VisualStudioActions.ShowDialogWindowAsync.
    /// </summary>
    public partial class MigrateWebFormDialog : UserControl
    {
        /// <summary>
        /// Raised by CodeFactory to dismiss the dialog window.
        /// CFX Rule 23: must be raised when workflow completes.
        /// </summary>
        public event CloseDialogEventHandler Close;

        public MigrateWebFormDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Appends a status message line to the progress panel.
        /// Thread-safe via Dispatcher.
        /// </summary>
        public void AddStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var text = new TextBlock
                {
                    Text = message,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Margin = new Thickness(0, 1, 0, 1),
                    TextWrapping = TextWrapping.Wrap
                };

                statusPanel.Children.Add(text);

                // Auto-scroll to bottom as messages arrive
                scrollViewer.ScrollToBottom();
            });
        }

        /// <summary>
        /// Called by the command when migration is fully complete.
        /// Enables the Close button and adds a completion message.
        /// CFX Rule 23: Close event must be raised — triggered when user clicks Close.
        /// </summary>
        public void SignalComplete()
        {
            Dispatcher.Invoke(() =>
            {
                AddStatus("── Migration complete. ──");
                btnClose.IsEnabled = true;
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // CFX Rule 23: raise Close to dismiss the VS-hosted dialog
            Close?.Invoke(this, new CloseDialogEventArgs());
        }
    }
}
