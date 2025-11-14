using InsightCleanerAI.Resources;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InsightCleanerAI.ViewModels;
using WinForms = System.Windows.Forms;

namespace InsightCleanerAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            await ViewModel.ScanAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CancelScan();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = Strings.BrowseRootDescription,
                SelectedPath = ViewModel.RootPath,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                ViewModel.RootPath = dialog.SelectedPath;
            }
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel is null)
            {
                return;
            }

            ViewModel.SelectedNode = e.NewValue as StorageNodeViewModel;
        }

        private void TreeView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.SelectedNode is null)
            {
                return;
            }

            if (GetAncestorTreeViewItem(e.OriginalSource as DependencyObject) is null)
            {
                return;
            }

            var node = ViewModel.SelectedNode;
            var fullPath = node.Model.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                MessageBox.Show(
                    Strings.MessagePrivacyOpenBlocked,
                    Strings.MessageCannotOpenTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.MessageOpenFailedFormat, fullPath, ex.Message),
                    Strings.MessageErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void TreeView_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;
                await DeleteSelectedNodeAsync();
            }
        }

        private void TreeView_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GetAncestorTreeViewItem(e.OriginalSource as DependencyObject) is TreeViewItem item)
            {
                item.IsSelected = true;
            }
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedNodeAsync();
        }

        private async Task DeleteSelectedNodeAsync()
        {
            if (ViewModel is null || ViewModel.SelectedNode is null)
            {
                return;
            }

            if (ViewModel.IsScanning)
            {
                MessageBox.Show(
                    Strings.MessageScanningDeletionBlocked,
                    Strings.MessageLaterTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var node = ViewModel.SelectedNode;
            var fullPath = node.Model.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                MessageBox.Show(
                    Strings.MessagePrivacyDeleteBlocked,
                    Strings.MessageCannotDeleteTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var targetLabel = node.Model.IsDirectory ? Strings.LabelDirectory : Strings.LabelFile;
            var confirmation = MessageBox.Show(
                string.Format(Strings.MessageDeleteConfirmFormat, targetLabel, node.DisplayPath),
                Strings.MessageDeleteDialogTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (node.Model.IsDirectory)
                {
                    Directory.Delete(fullPath, true);
                }
                else
                {
                    File.Delete(fullPath);
                }

                if (!ViewModel.TryRemoveNode(node))
                {
                    await ViewModel.ScanAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.MessageDeleteError, ex.Message),
                    Strings.MessageErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenSettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow
            {
                Owner = this,
                DataContext = DataContext
            };
            window.ShowDialog();
        }

        private void OpenBlacklistMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = new BlacklistWindow
            {
                Owner = this,
                DataContext = DataContext
            };
            window.ShowDialog();
        }

        private static TreeViewItem? GetAncestorTreeViewItem(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is TreeViewItem item)
                {
                    return item;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}


