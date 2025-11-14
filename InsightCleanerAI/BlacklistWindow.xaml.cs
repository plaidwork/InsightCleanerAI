using System.Windows;

namespace InsightCleanerAI
{
    public partial class BlacklistWindow : Window
    {
        public BlacklistWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
