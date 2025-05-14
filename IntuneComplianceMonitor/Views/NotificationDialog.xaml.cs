using System.Windows;

namespace IntuneComplianceMonitor.Views
{
    public partial class NotificationDialog : Window
    {
        public string NotificationTitle { get; private set; }
        public string NotificationMessage { get; private set; }

        public NotificationDialog(string defaultTitle, string defaultMessage, Window owner)
        {
            InitializeComponent();

            // Set window properties
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Set default values directly after InitializeComponent
            TitleTextBox.Text = defaultTitle;
            MessageTextBox.Text = defaultMessage;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                MessageBox.Show("Notification message cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NotificationTitle = TitleTextBox.Text;
            NotificationMessage = MessageTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}