using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PicDispatch.Models;
using PicDispatch.Services;

namespace PicDispatch.ViewModels
{
    public class TrashViewModel : ObservableObject
    {
        public RelayCommand RefreshCommand { get; }
        private readonly FileActionService _fileActionService;
        private readonly ImageLoaderService _imageLoaderService;
        private readonly MainViewModel _mainViewModel;
        private TrashItem _selectedTrashItem;
        private TargetFolder _selectedTarget;
        private string _notificationText = string.Empty;
        private int _thumbnailRefreshVersion;

        public TrashViewModel(
            FileActionService fileActionService,
            MainViewModel mainViewModel,
            ImageLoaderService imageLoaderService,
            IEnumerable<TargetFolder> targets)
        {
            _fileActionService = fileActionService;
            _mainViewModel = mainViewModel;
            _imageLoaderService = imageLoaderService;
            _fileActionService.StateChanged += OnFileActionStateChanged;
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

            TrashItems = new ObservableCollection<TrashItem>();
            Targets = new ObservableCollection<TargetFolder>(targets ?? Enumerable.Empty<TargetFolder>());

            ClassifyCommand = new RelayCommand(ClassifySelected, () => CanClassify);
            RemoveCommand = new RelayCommand(RemoveSelected, () => CanRemove);
            ClearTrashCommand = new RelayCommand(ClearTrash, () => CanClearTrash);
            RefreshCommand = new RelayCommand(Refresh);
            RestoreCommand = new RelayCommand(Restore, () => CanRestore);
            SelectedTarget = Targets.FirstOrDefault();
            RefreshTrashItems();
        }

        public ObservableCollection<TrashItem> TrashItems { get; }

        public ObservableCollection<TargetFolder> Targets { get; }

        public TrashItem SelectedTrashItem
        {
            get => _selectedTrashItem;
            set
            {
                if (SetProperty(ref _selectedTrashItem, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public TargetFolder SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (SetProperty(ref _selectedTarget, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string NotificationText
        {
            get => _notificationText;
            private set
            {
                if (SetProperty(ref _notificationText, value))
                {
                    OnPropertyChanged(nameof(HasNotification));
                }
            }
        }

        public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationText);

        public bool CanClassify => SelectedTrashItem != null && SelectedTarget != null;

        public bool CanRemove => SelectedTrashItem != null;

        public bool CanClearTrash => TrashItems.Count > 0;

        public bool CanRestore => SelectedTrashItem != null;

        public RelayCommand ClassifyCommand { get; }

        public RelayCommand RemoveCommand { get; }

        public RelayCommand ClearTrashCommand { get; }

        public RelayCommand RestoreCommand { get; }

        public void Dispose()
        {
            _fileActionService.StateChanged -= OnFileActionStateChanged;
            _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        public void Refresh()
        {
            _fileActionService.PruneMissingTrashItems();
            RefreshTrashItems();
        }

        private void ClassifySelected()
        {
            if (!CanClassify)
            {
                return;
            }

            var item = SelectedTrashItem;
            try
            {
                var conflictResolution = _fileActionService.DestinationExists(item, SelectedTarget)
                    ? MoveConflictResolution.AppendNumber
                    : MoveConflictResolution.Cancel;
                _fileActionService.ClassifyFromTrash(item, SelectedTarget, conflictResolution);
                ShowNotification($"Classified to {SelectedTarget.Name}：{item.FileName}");
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to classify: {ex.Message}");
            }
        }

        private void RemoveSelected()
        {
            if (!CanRemove)
            {
                return;
            }

            var item = SelectedTrashItem;
            try
            {
                _fileActionService.RemoveFromTrash(item);
                ShowNotification($"{item.FileName} deleted");
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to delete：{ex.Message}");
            }
        }

        private void ClearTrash()
        {
            if (!CanClearTrash)
            {
                return;
            }

            try
            {
                var count = TrashItems.Count;
                _fileActionService.ClearTrash();
                ShowNotification($"Successfully cleared trash: {count} items.");
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to clear trash: {ex.Message}");
            }
        }

        private void Restore()
        {
            if (!CanRestore)
            {
                return;
            }

            var item = SelectedTrashItem;
            try
            {
                _fileActionService.RestoreFromTrash(item);
                _mainViewModel.RefreshCommand.Execute(null);
                ShowNotification($"Restored: {item.FileName}");
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to restore: {ex.Message}");
            }
        }

        private void RefreshTrashItems()
        {
            var selectedPath = SelectedTrashItem?.TrashPath;
            var refreshVersion = ++_thumbnailRefreshVersion;
            TrashItems.Clear();
            foreach (var item in _fileActionService.TrashItems)
            {
                TrashItems.Add(item);
            }

            SelectedTrashItem = TrashItems.FirstOrDefault(item =>
                                    string.Equals(item.TrashPath, selectedPath, StringComparison.OrdinalIgnoreCase)) ??
                                TrashItems.FirstOrDefault();
            RaiseCommandStates();

            foreach (var item in TrashItems)
            {
                LoadThumbnailAsync(item, refreshVersion);
            }
        }

        private async void LoadThumbnailAsync(TrashItem item, int refreshVersion)
        {
            try
            {
                var thumbnail = await Task.Run(() => _imageLoaderService.LoadThumbnail(item.TrashPath, 48));
                if (refreshVersion == _thumbnailRefreshVersion && TrashItems.Contains(item))
                {
                    item.Thumbnail = thumbnail;
                }
            }
            catch
            {
                if (refreshVersion == _thumbnailRefreshVersion && TrashItems.Contains(item))
                {
                    item.Thumbnail = null;
                }
            }
        }

        private void OnFileActionStateChanged()
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(OnFileActionStateChanged));
                return;
            }

            RefreshTrashItems();
        }

        private void OnMainViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CanUndo) ||
                e.PropertyName == nameof(MainViewModel.InteractionState))
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(RaiseCommandStates));
                    return;
                }

                RaiseCommandStates();
            }
        }

        private void RaiseCommandStates()
        {
            OnPropertyChanged(nameof(CanClassify));
            OnPropertyChanged(nameof(CanRemove));
            OnPropertyChanged(nameof(CanClearTrash));
            OnPropertyChanged(nameof(CanRestore));
            ClassifyCommand?.RaiseCanExecuteChanged();
            RemoveCommand?.RaiseCanExecuteChanged();
            ClearTrashCommand?.RaiseCanExecuteChanged();
            RestoreCommand?.RaiseCanExecuteChanged();
        }

        private async void ShowNotification(string message)
        {
            NotificationText = message;
            await Task.Delay(3000);
            if (NotificationText == message)
            {
                NotificationText = string.Empty;
            }
        }
    }
}
