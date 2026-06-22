using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using PicDispatch.Models;
using PicDispatch.Services;

namespace PicDispatch.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly ImageQueueService _queueService;
        private readonly ImageLoaderService _imageLoaderService;
        private readonly FileActionService _fileActionService;
        private readonly List<ImageItem> _queue = new List<ImageItem>();
        private AppSettings _settings;
        private ImageItem _currentItem;
        private BitmapSource _currentImage;
        private int _currentIndex = -1;
        private string _selectedSourceFolder;
        private string _statusText = "No folders configured.";
        private string _notificationText = string.Empty;
        private AppInteractionState _interactionState = AppInteractionState.Browsing;

        public MainViewModel(
            SettingsService settingsService,
            ImageQueueService queueService,
            ImageLoaderService imageLoaderService,
            FileActionService fileActionService)
        {
            _settingsService = settingsService;
            _queueService = queueService;
            _imageLoaderService = imageLoaderService;
            _fileActionService = fileActionService;
            _fileActionService.StateChanged += OnFileActionStateChanged;

            Targets = new ObservableCollection<TargetFolder>();
            SourceFolders = new ObservableCollection<SourceFolderViewModel>();
            UndoCommand = new RelayCommand(Undo, () => CanUndo);
            TrashCurrentCommand = new RelayCommand(() => _ = DeleteCurrentAsync(), () => CanDeleteCurrent);
            OpenTrashCommand = new RelayCommand(() => RequestTrash?.Invoke());
            SelectSourceFolderCommand = new RelayCommand(SelectSourceFolder);
            MoveToTargetCommand = new RelayCommand(target => _ = MoveCurrentAsync(target as TargetFolder), target => target is TargetFolder && CanDeleteCurrent);
            RefreshCommand = new RelayCommand(LoadQueue);
        }

        public event Action RequestSettings;

        public event Action RequestTrash;

        public event Func<ImageItem, TargetFolder, bool?> RequestMoveConflict;

        public AppSettings Settings => _settings;

        public FileActionService FileActions => _fileActionService;

        public ObservableCollection<TargetFolder> Targets { get; }

        public ObservableCollection<SourceFolderViewModel> SourceFolders { get; }

        public ImageItem CurrentItem
        {
            get => _currentItem;
            private set
            {
                if (SetProperty(ref _currentItem, value))
                {
                    OnPropertyChanged(nameof(CurrentFileName));
                    OnPropertyChanged(nameof(CurrentSourceFolder));
                    OnPropertyChanged(nameof(HasImage));
                    OnPropertyChanged(nameof(CanDeleteCurrent));
                    OnPropertyChanged(nameof(CanJump));
                    OnPropertyChanged(nameof(ActionStatusText));
                    TrashCurrentCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public BitmapSource CurrentImage
        {
            get => _currentImage;
            private set => SetProperty(ref _currentImage, value);
        }

        public string CurrentFileName => CurrentItem?.FileName ?? "No image selected";

        public string CurrentSourceFolder => CurrentItem?.SourceFolder ?? string.Empty;

        public bool HasImage => CurrentItem != null;

        public int QueueCount => _queue.Count;

        public double ProgressMaximum => Math.Max(QueueCount, 1);

        public string CurrentPositionText => QueueCount > 0 ? $"{CurrentPosition:0} / {QueueCount}" : "0 / 0";

        public double CurrentPosition
        {
            get => _currentIndex >= 0 ? _currentIndex + 1 : 1;
            set
            {
                if (!CanJump)
                {
                    return;
                }

                JumpToIndex((int)Math.Round(value) - 1, true);
            }
        }

        public bool CanJump => HasImage && InteractionState == AppInteractionState.Browsing;

        public int TrashCount => _fileActionService.TrashCount;

        public bool HasTrashItems => TrashCount > 0;

        public int UndoCount => _fileActionService.UndoCount;

        public bool IsConfigured => _settings != null &&
                                    _settings.SourceFolders.Count > 0 &&
                                    _settings.TargetFolders.Count > 0;

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string NotificationText
        {
            get => _notificationText;
            private set
            {
                if (SetProperty(ref _notificationText, value))
                {
                    OnPropertyChanged(nameof(HasNotification));
                    OnPropertyChanged(nameof(ActionStatusText));
                }
            }
        }

        public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationText);

        public string ActionStatusText => HasNotification ? NotificationText : DefaultActionHint;

        private string DefaultActionHint
        {
            get
            {
                if (!HasImage || Targets.Count == 0)
                {
                    return string.Empty;
                }

                var shortcuts = Targets
                    .Where(target => !string.IsNullOrWhiteSpace(target.Shortcut))
                    .Select(target => target.Shortcut)
                    .ToArray();
                var shortcutText = shortcuts.Length > 0 ? string.Join("/", shortcuts) : "Target Shortcut";
                var undoText = $"{UndoCount} action{(UndoCount == 1 ? string.Empty : "s")} undoable";
                return HasImage
                    ? $"Press {shortcutText} to sort, Delete to move to Trash, ←/→ to switch images · {undoText}"
                    : undoText;
            }
        }

        public AppInteractionState InteractionState
        {
            get => _interactionState;
            private set
            {
                if (SetProperty(ref _interactionState, value))
                {
                    OnPropertyChanged(nameof(IsMoving));
                    OnPropertyChanged(nameof(CanJump));
                    OnPropertyChanged(nameof(CanDeleteCurrent));
                }
            }
        }

        public bool IsMoving => InteractionState == AppInteractionState.Moving;

        public bool CanUndo => _fileActionService.CanUndo && InteractionState == AppInteractionState.Browsing;

        public bool CanDeleteCurrent => HasImage && InteractionState == AppInteractionState.Browsing;

        public RelayCommand UndoCommand { get; }

        public RelayCommand TrashCurrentCommand { get; }

        public RelayCommand OpenTrashCommand { get; }

        public RelayCommand SelectSourceFolderCommand { get; }

        public RelayCommand MoveToTargetCommand { get; }

        public RelayCommand RefreshCommand { get; }

        public void Initialize()
        {
            ApplySettings(_settingsService.Load());
        }

        public void OpenSettings()
        {
            RequestSettings?.Invoke();
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            OnPropertyChanged(nameof(IsConfigured));
            Targets.Clear();
            foreach (var target in settings.TargetFolders)
            {
                Targets.Add(target);
            }

            OnPropertyChanged(nameof(ActionStatusText));

            SourceFolders.Clear();
            foreach (var sourceFolder in settings.SourceFolders)
            {
                SourceFolders.Add(new SourceFolderViewModel(sourceFolder));
            }

            if (_selectedSourceFolder == null ||
                !settings.SourceFolders.Any(folder => string.Equals(folder, _selectedSourceFolder, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedSourceFolder = settings.SourceFolders.FirstOrDefault();
            }

            LoadQueue();
        }

        public void SaveSettings()
        {
            if (_settings != null)
            {
                _settingsService.Save(_settings);
            }
        }

        public TargetFolder FindTargetByShortcut(string shortcut)
        {
            return Targets.FirstOrDefault(target =>
                string.Equals(target.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase));
        }

        private void SelectSourceFolder(object parameter)
        {
            var sourceFolder = parameter as SourceFolderViewModel;
            if (sourceFolder == null ||
                string.Equals(_selectedSourceFolder, sourceFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedSourceFolder = sourceFolder.Path;
            RefreshSourceFolderState();
            _currentIndex = FindFirstVisibleIndex();
            if (_currentIndex < 0)
            {
                _currentIndex = 0;
                SyncSelectedSourceFolderToCurrent();
                RefreshSourceFolderState();
            }
            RaiseQueuePositionChanged();
            _ = LoadCurrentImageAsync();
        }

        public void ShowPrevious()
        {
            if (CanJump)
            {
                JumpToIndex(_currentIndex - 1, true);
            }
        }

        public void ShowNext()
        {
            if (CanJump)
            {
                JumpToIndex(_currentIndex + 1, true);
            }
        }

        public async Task MoveCurrentAsync(TargetFolder target)
        {
            if (CurrentItem == null || target == null || InteractionState != AppInteractionState.Browsing)
            {
                return;
            }

            var item = CurrentItem;
            MoveConflictResolution conflictResolution = MoveConflictResolution.Cancel;
            if (_fileActionService.DestinationExists(item, target))
            {
                var appendNumber = RequestMoveConflict?.Invoke(item, target);
                if (appendNumber != true)
                {
                    ShowNotification("移动已取消。");
                    return;
                }

                conflictResolution = MoveConflictResolution.AppendNumber;
            }

            InteractionState = AppInteractionState.Moving;
            StatusText = $"Moving {item.FileName} -> {target.Name}";
            RaiseCommandStates();

            try
            {
                await Task.Run(() => _fileActionService.MoveToTarget(item, target, conflictResolution, _currentIndex));
                RemoveCurrentAndAdvance();
                ShowNotification($"已移动到 {target.Name}");
            }
            catch (Exception ex)
            {
                if (!File.Exists(item.Path))
                {
                    StatusText = $"Source missing: {item.FileName}";
                    RemoveMissingItemAndAdvance(item);
                    ShowNotification($"Source file missing, skipped: {item.FileName}");
                    return;
                }

                StatusText = $"Move failed: {item.FileName}";
                ShowNotification($"移动失败：{ex.Message}");
                await LoadCurrentImageAsync();
            }
            finally
            {
                InteractionState = AppInteractionState.Browsing;
                RaiseCommandStates();
            }
        }

        private async Task DeleteCurrentAsync()
        {
            if (CurrentItem == null || InteractionState != AppInteractionState.Browsing)
            {
                return;
            }

            var item = CurrentItem;
            InteractionState = AppInteractionState.Moving;
            StatusText = $"Deleting {item.FileName}";
            RaiseCommandStates();

            try
            {
                await Task.Run(() => _fileActionService.MoveToTrash(item, _currentIndex));
                RemoveCurrentAndAdvance();
                ShowNotification($"Moved to Trash: {item.FileName}");
            }
            catch (Exception ex)
            {
                if (!File.Exists(item.Path))
                {
                    StatusText = $"Source missing: {item.FileName}";
                    RemoveMissingItemAndAdvance(item);
                    ShowNotification($"Source file missing, skipped: {item.FileName}");
                    return;
                }

                StatusText = $"Delete failed: {item.FileName}";
                ShowNotification($"Failed to move to Trash: {ex.Message}");
                await LoadCurrentImageAsync();
            }
            finally
            {
                InteractionState = AppInteractionState.Browsing;
                RaiseCommandStates();
            }
        }

        private void LoadQueue()
        {
            _queue.Clear();
            CurrentImage = null;
            CurrentItem = null;
            _currentIndex = -1;
            RaiseQueuePositionChanged();

            if (_settings == null || _settings.SourceFolders.Count == 0 || _settings.TargetFolders.Count == 0)
            {
                StatusText = string.Empty;
                return;
            }

            _queue.AddRange(_queueService.BuildQueue(_settings.SourceFolders));
            RefreshSourceFolderState();
            RaiseQueuePositionChanged();
            if (_queue.Count == 0)
            {
                StatusText = "没有可处理的图片。";
                return;
            }

            _currentIndex = FindFirstVisibleIndex();
            RaiseQueuePositionChanged();
            _ = LoadCurrentImageAsync();
        }

        private async Task LoadCurrentImageAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= _queue.Count)
            {
                CurrentItem = null;
                CurrentImage = null;
                StatusText = "没有可处理的图片。";
                RaiseQueuePositionChanged();
                return;
            }

            var loadIndex = _currentIndex;
            var item = _queue[loadIndex];
            CurrentItem = item;
            StatusText = $"{_currentIndex + 1} / {_queue.Count}";
            RaiseQueuePositionChanged();

            try
            {
                var image = await Task.Run(() => _imageLoaderService.Load(item.Path));
                if (loadIndex == _currentIndex && CurrentItem != null && CurrentItem.Path == item.Path)
                {
                    CurrentImage = image;
                }
            }
            catch (Exception ex)
            {
                if (loadIndex == _currentIndex && CurrentItem != null && CurrentItem.Path == item.Path)
                {
                    CurrentImage = null;
                    ShowNotification($"图片加载失败：{ex.Message}");
                }
            }
        }

        private void RemoveCurrentAndAdvance()
        {
            if (_currentIndex < 0 || _currentIndex >= _queue.Count)
            {
                return;
            }

            _queue.RemoveAt(_currentIndex);
            RefreshSourceFolderState();
            if (_currentIndex >= _queue.Count)
            {
                _currentIndex = _queue.Count - 1;
            }

            SyncSelectedSourceFolderToCurrent();
            RefreshSourceFolderState();
            RaiseQueuePositionChanged();
            _ = LoadCurrentImageAsync();
        }

        private void RemoveMissingItemAndAdvance(ImageItem item)
        {
            var removeIndex = _currentIndex >= 0 &&
                              _currentIndex < _queue.Count &&
                              string.Equals(_queue[_currentIndex].Path, item.Path, StringComparison.OrdinalIgnoreCase)
                ? _currentIndex
                : _queue.FindIndex(candidate => string.Equals(candidate.Path, item.Path, StringComparison.OrdinalIgnoreCase));

            if (removeIndex < 0)
            {
                _ = LoadCurrentImageAsync();
                return;
            }

            _queue.RemoveAt(removeIndex);
            if (_currentIndex > removeIndex)
            {
                _currentIndex--;
            }
            else if (_currentIndex >= _queue.Count)
            {
                _currentIndex = _queue.Count - 1;
            }

            SyncSelectedSourceFolderToCurrent();
            RefreshSourceFolderState();
            RaiseQueuePositionChanged();
            _ = LoadCurrentImageAsync();
        }

        private void JumpToIndex(int index, bool updateSelectedSourceFolder)
        {
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= _queue.Count)
            {
                index = _queue.Count - 1;
            }

            if (index == _currentIndex)
            {
                return;
            }

            _currentIndex = index;
            if (updateSelectedSourceFolder)
            {
                SyncSelectedSourceFolderToCurrent();
                RefreshSourceFolderState();
            }

            RaiseQueuePositionChanged();
            _ = LoadCurrentImageAsync();
        }

        private int FindFirstVisibleIndex()
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                if (IsVisibleInSelectedSource(_queue[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsVisibleInSelectedSource(ImageItem item)
        {
            return item != null &&
                   string.Equals(item.SourceFolder, _selectedSourceFolder, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshSourceFolderState()
        {
            foreach (var sourceFolder in SourceFolders)
            {
                sourceFolder.RemainingCount = _queue.Count(item =>
                    string.Equals(item.SourceFolder, sourceFolder.Path, StringComparison.OrdinalIgnoreCase));
                sourceFolder.IsSelected = string.Equals(sourceFolder.Path, _selectedSourceFolder, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SyncSelectedSourceFolderToCurrent()
        {
            if (_currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                _selectedSourceFolder = _queue[_currentIndex].SourceFolder;
            }
        }

        private void Undo()
        {
            try
            {
                var record = _fileActionService.Undo();
                if (record == null)
                {
                    return;
                }

                if (record.Kind == FileActionKind.MoveToTarget || record.Kind == FileActionKind.MoveToTrash)
                {
                    var item = new ImageItem(record.OriginalPath, record.SourceFolder);
                    var insertIndex = Math.Max(record.QueueIndex, 0);
                    if (insertIndex > _queue.Count)
                    {
                        insertIndex = _queue.Count;
                    }

                    _queue.Insert(insertIndex, item);
                    _currentIndex = insertIndex;
                    SyncSelectedSourceFolderToCurrent();
                    RefreshSourceFolderState();
                    RaiseQueuePositionChanged();
                    _ = LoadCurrentImageAsync();
                }

                ShowNotification(GetUndoMessage(record.Kind));
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to undo: {ex.Message}");
            }
            finally
            {
                RaiseCommandStates();
            }
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

        private void RaiseCommandStates()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanDeleteCurrent));
            OnPropertyChanged(nameof(CanJump));
            OnPropertyChanged(nameof(TrashCount));
            OnPropertyChanged(nameof(HasTrashItems));
            OnPropertyChanged(nameof(UndoCount));
            OnPropertyChanged(nameof(ActionStatusText));
            UndoCommand.RaiseCanExecuteChanged();
            TrashCurrentCommand.RaiseCanExecuteChanged();
            MoveToTargetCommand.RaiseCanExecuteChanged();
        }

        private void RaiseQueuePositionChanged()
        {
            OnPropertyChanged(nameof(QueueCount));
            OnPropertyChanged(nameof(ProgressMaximum));
            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(CurrentPositionText));
            OnPropertyChanged(nameof(CanJump));
        }

        private void OnFileActionStateChanged()
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(OnFileActionStateChanged));
                return;
            }

            OnPropertyChanged(nameof(TrashCount));
            OnPropertyChanged(nameof(HasTrashItems));
            OnPropertyChanged(nameof(UndoCount));
            OnPropertyChanged(nameof(ActionStatusText));
            RaiseCommandStates();
        }

        private static string GetUndoMessage(FileActionKind kind)
        {
            switch (kind)
            {
                case FileActionKind.MoveToTrash:
                    return "Undo: Restored from Trash";
                case FileActionKind.ClassifyFromTrash:
                    return "Undo: Classified from Trash";
                case FileActionKind.RemoveFromTrash:
                    return "Undo: Removed from Trash";
                case FileActionKind.ClearTrash:
                    return "Undo: Cleared Trash";
                default:
                    return "Undo: Moved";
            }
        }
    }
}
