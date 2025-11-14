using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using InsightCleanerAI.Resources;
using InsightCleanerAI.ViewModels;
using WinForms = System.Windows.Forms;

namespace InsightCleanerAI
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.CloudApiKey = passwordBox.Password;
        }

        private void SearchApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.SearchApiKey = passwordBox.Password;
        }

        private void LocalLlmApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.LocalLlmApiKey = passwordBox.Password;
        }

        private void MaxNodesButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ApplyMaxNodesPreset();
        }

        private void BrowseCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = Strings.BrowseCacheDialogDescription,
                SelectedPath = Directory.Exists(ViewModel.CacheDirectory)
                    ? ViewModel.CacheDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                ViewModel.CacheDirectory = dialog.SelectedPath;
            }
        }

        private void BrowseDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = Strings.BrowseDatabaseDialogTitle,
                FileName = Path.GetFileName(ViewModel.DatabasePath),
                InitialDirectory = GetDatabaseDirectory(),
                Filter = Strings.DatabaseFileFilter,
                AddExtension = true,
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                ViewModel.DatabasePath = dialog.FileName;
            }
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                Strings.ClearCacheConfirm,
                Strings.ClearCacheTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            if (ViewModel.TryClearCache(out var message))
            {
                MessageBox.Show(message, Strings.ClearCacheDoneTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, Strings.ClearCacheErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            ViewModel.SaveConfiguration(includeSensitive: false);
            MessageBox.Show(
                Strings.SaveDefaultsMessage,
                Strings.ClearCacheDoneTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SaveConfiguration();
            Close();
        }


        private string GetDatabaseDirectory()
        {
            if (ViewModel is null)
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            var directory = Path.GetDirectoryName(ViewModel.DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
    }
}

