using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly FileMoveService _fileMoveService;
        private readonly List<ImageItem> _queue = new List<ImageItem>();
        private AppSettings _settings;
        private ImageItem _currentItem;
        private BitmapSource _currentImage;
        private int _currentIndex = -1;
        private string _statusText = "No folders configured.";
        private string _notificationText = string.Empty;
        private AppInteractionState _interactionState = AppInteractionState.Browsing;

        public MainViewModel(
            SettingsService settingsService,
            ImageQueueService queueService,
            ImageLoaderService imageLoaderService,
            FileMoveService fileMoveService)
        {
            _settingsService = settingsService;
            _queueService = queueService;
            _imageLoaderService = imageLoaderService;
            _fileMoveService = fileMoveService;

            Targets = new ObservableCollection<TargetFolder>();
            UndoCommand = new RelayCommand(Undo, () => CanUndo);
            RefreshCommand = new RelayCommand(LoadQueue);
        }

        public event Action RequestSettings;

        public event Func<ImageItem, TargetFolder, bool?> RequestMoveConflict;

        public AppSettings Settings => _settings;

        public ObservableCollection<TargetFolder> Targets { get; }

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
                }
            }
        }

        public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationText);

        public AppInteractionState InteractionState
        {
            get => _interactionState;
            private set
            {
                if (SetProperty(ref _interactionState, value))
                {
                    OnPropertyChanged(nameof(IsMoving));
                }
            }
        }

        public bool IsMoving => InteractionState == AppInteractionState.Moving;

        public bool CanUndo => _fileMoveService.CanUndo && InteractionState == AppInteractionState.Browsing;

        public RelayCommand UndoCommand { get; }

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

        public async Task MoveCurrentAsync(TargetFolder target)
        {
            if (CurrentItem == null || target == null || InteractionState != AppInteractionState.Browsing)
            {
                return;
            }

            var item = CurrentItem;
            MoveConflictResolution conflictResolution = MoveConflictResolution.Cancel;
            if (_fileMoveService.DestinationExists(item, target))
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
                await Task.Run(() => _fileMoveService.Move(item, target, conflictResolution));
                RemoveCurrentAndAdvance();
                ShowNotification($"已移动到 {target.Name}");
            }
            catch (Exception ex)
            {
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

        private void LoadQueue()
        {
            _queue.Clear();
            CurrentImage = null;
            CurrentItem = null;
            _currentIndex = -1;

            if (_settings == null || _settings.SourceFolders.Count == 0 || _settings.TargetFolders.Count == 0)
            {
                StatusText = string.Empty;
                return;
            }

            _queue.AddRange(_queueService.BuildQueue(_settings.SourceFolders));
            if (_queue.Count == 0)
            {
                StatusText = "没有可处理的图片。";
                return;
            }

            _currentIndex = 0;
            _ = LoadCurrentImageAsync();
        }

        private async Task LoadCurrentImageAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= _queue.Count)
            {
                CurrentItem = null;
                CurrentImage = null;
                StatusText = "没有可处理的图片。";
                return;
            }

            var item = _queue[_currentIndex];
            CurrentItem = item;
            StatusText = $"{_currentIndex + 1} / {_queue.Count}";

            try
            {
                CurrentImage = await Task.Run(() => _imageLoaderService.Load(item.Path));
            }
            catch (Exception ex)
            {
                CurrentImage = null;
                ShowNotification($"图片加载失败：{ex.Message}");
            }
        }

        private void RemoveCurrentAndAdvance()
        {
            if (_currentIndex < 0 || _currentIndex >= _queue.Count)
            {
                return;
            }

            _queue.RemoveAt(_currentIndex);
            if (_currentIndex >= _queue.Count)
            {
                _currentIndex = _queue.Count - 1;
            }

            _ = LoadCurrentImageAsync();
        }

        private void Undo()
        {
            try
            {
                var record = _fileMoveService.Undo();
                if (record == null)
                {
                    return;
                }

                var item = new ImageItem(record.OriginalPath, record.SourceFolder);
                var insertIndex = Math.Max(_currentIndex, 0);
                if (insertIndex > _queue.Count)
                {
                    insertIndex = _queue.Count;
                }

                _queue.Insert(insertIndex, item);
                _currentIndex = insertIndex;
                _ = LoadCurrentImageAsync();
                ShowNotification("已撤回上一次移动。");
            }
            catch (Exception ex)
            {
                ShowNotification($"撤回失败：{ex.Message}");
            }
            finally
            {
                RaiseCommandStates();
            }
        }

        private async void ShowNotification(string message)
        {
            NotificationText = message;
            await Task.Delay(2800);
            if (NotificationText == message)
            {
                NotificationText = string.Empty;
            }
        }

        private void RaiseCommandStates()
        {
            OnPropertyChanged(nameof(CanUndo));
            UndoCommand.RaiseCanExecuteChanged();
        }
    }
}
