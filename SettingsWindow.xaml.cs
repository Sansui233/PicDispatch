using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PicDispatch.Models;
using PicDispatch.Services;
using PicDispatch.ViewModels;

namespace PicDispatch
{
    public partial class SettingsWindow : Window
    {
        private readonly FolderPickerService _folderPickerService = new FolderPickerService();

        public SettingsWindow(AppSettings settings, System.Action<AppSettings> settingsChanged)
        {
            InitializeComponent();
            SettingsChanged = settingsChanged;
            ViewModel = new SettingsViewModel(settings);
            DataContext = ViewModel;
        }

        public SettingsViewModel ViewModel { get; }

        private System.Action<AppSettings> SettingsChanged { get; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.ApplyWindows11Chrome(this);
            WindowEffects.CenterOnCurrentScreen(this);
        }

        private void AddSource_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFolder(null);
            if (!string.IsNullOrWhiteSpace(path))
            {
                ViewModel.AddSource(path);
                CommitSettings();
            }
        }

        private void DeleteSourceMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var placementTarget = contextMenu?.PlacementTarget as FrameworkElement;
            if (!(placementTarget?.DataContext is FolderEntry source))
            {
                return;
            }

            ViewModel.Sources.Remove(source);
            CommitSettings();
        }

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFolder(null);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var shortcutDialog = new ShortcutInputDialog(path, ViewModel.Targets.Select(target => target.Shortcut))
            {
                Owner = this
            };
            if (shortcutDialog.ShowDialog() != true)
            {
                return;
            }

            ViewModel.AddTarget(path, shortcutDialog.Shortcut);
            CommitSettings();
        }

        private void DeleteTargetMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var placementTarget = contextMenu?.PlacementTarget as FrameworkElement;
            if (!(placementTarget?.DataContext is TargetFolderViewModel target))
            {
                return;
            }

            ViewModel.Targets.Remove(target);
            CommitSettings();
        }

        private void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is TargetFolderViewModel target)
            {
                var reservedShortcuts = ViewModel.Targets
                    .Where(item => item != target)
                    .Select(item => item.Shortcut);
                var shortcutDialog = new ShortcutInputDialog(target.FolderPath, reservedShortcuts, target.Shortcut)
                {
                    Owner = this
                };

                if (shortcutDialog.ShowDialog() != true)
                {
                    return;
                }

                target.Shortcut = shortcutDialog.Shortcut;
                CommitSettings();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CommitSettings();
            Close();
        }

        private string PickFolder(string initialPath)
        {
            return _folderPickerService.PickFolder(this, initialPath);
        }

        private void CommitSettings()
        {
            if (!ViewModel.TryBuildSettings(out var settings))
            {
                return;
            }

            SettingsChanged?.Invoke(settings);
        }
    }
}
