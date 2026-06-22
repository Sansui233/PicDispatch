using System;
using System.Windows;
using System.Windows.Input;
using PicDispatch.Models;
using PicDispatch.Services;
using PicDispatch.ViewModels;

namespace PicDispatch
{
    public partial class MainWindow : Window
    {
        private readonly ShortcutService _shortcutService = new ShortcutService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly ImageLoaderService _imageLoaderService = new ImageLoaderService();
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(
                _settingsService,
                new ImageQueueService(),
                _imageLoaderService,
                new FileActionService());
            _viewModel.RequestSettings += OpenSettings;
            _viewModel.RequestTrash += OpenTrash;
            _viewModel.RequestMoveConflict += ConfirmMoveConflict;
            DataContext = _viewModel;
            _viewModel.Initialize();
            ApplyWindowSize();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.ApplyWindows11Chrome(this);
            WindowEffects.CenterOnCurrentScreen(this);
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_shortcutService.CanUseBrowserShortcut(_viewModel.InteractionState))
            {
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (_viewModel.UndoCommand.CanExecute(null))
                {
                    _viewModel.UndoCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (_viewModel.TrashCurrentCommand.CanExecute(null))
                {
                    _viewModel.TrashCurrentCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                _viewModel.ShowPrevious();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                _viewModel.ShowNext();
                e.Handled = true;
                return;
            }

            var shortcut = _shortcutService.Normalize(e);
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                return;
            }

            var target = _viewModel.FindTargetByShortcut(shortcut);
            if (target == null)
            {
                return;
            }

            e.Handled = true;
            await _viewModel.MoveCurrentAsync(target);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = _viewModel.Settings;
            if (settings == null)
            {
                return;
            }

            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            _settingsService.Save(settings);
        }

        private void OpenSettings()
        {
            var window = new SettingsWindow(_viewModel.Settings ?? new AppSettings(), settings =>
                {
                    _viewModel.ApplySettings(settings);
                    _viewModel.SaveSettings();
                })
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void OpenTrash()
        {
            var window = new TrashWindow(_viewModel.Settings?.TargetFolders, _viewModel, _imageLoaderService)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private bool? ConfirmMoveConflict(ImageItem item, TargetFolder target)
        {
            var dialog = new DuplicateConflictDialog(item.FileName, target.Name)
            {
                Owner = this
            };
            return dialog.ShowDialog();
        }

        private void ApplyWindowSize()
        {
            var settings = _viewModel.Settings;
            if (settings == null)
            {
                return;
            }

            if (!double.IsNaN(settings.WindowWidth) && settings.WindowWidth >= MinWidth)
            {
                Width = settings.WindowWidth;
            }

            if (!double.IsNaN(settings.WindowHeight) && settings.WindowHeight >= MinHeight)
            {
                Height = settings.WindowHeight;
            }
        }
    }
}
