using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PicDispatch.Models;

namespace PicDispatch.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private string _validationMessage = string.Empty;
        private FolderEntry _selectedSource;
        private TargetFolderViewModel _selectedTarget;

        public SettingsViewModel(AppSettings settings)
        {
            Sources = new ObservableCollection<FolderEntry>(
                settings.SourceFolders.Select(path => new FolderEntry { Path = path }));
            Targets = new ObservableCollection<TargetFolderViewModel>(
                settings.TargetFolders.Select(TargetFolderViewModel.FromModel));
            CurrentSettings = settings;

            RemoveSourceCommand = new RelayCommand(() =>
            {
                if (SelectedSource != null)
                {
                    Sources.Remove(SelectedSource);
                }
            }, () => SelectedSource != null);

            RemoveTargetCommand = new RelayCommand(() =>
            {
                if (SelectedTarget != null)
                {
                    Targets.Remove(SelectedTarget);
                }
            }, () => SelectedTarget != null);
        }

        public ObservableCollection<FolderEntry> Sources { get; }

        public ObservableCollection<TargetFolderViewModel> Targets { get; }

        public AppSettings CurrentSettings { get; private set; }

        public FolderEntry SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (SetProperty(ref _selectedSource, value))
                {
                    RemoveSourceCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public TargetFolderViewModel SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (SetProperty(ref _selectedTarget, value))
                {
                    RemoveTargetCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }

        public RelayCommand RemoveSourceCommand { get; }

        public RelayCommand RemoveTargetCommand { get; }

        public void AddSource(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var existing = Sources.FirstOrDefault(source =>
                string.Equals(source.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedSource = existing;
                ValidationMessage = "Source folder 已存在。";
                return;
            }

            var entry = new FolderEntry { Path = path };
            Sources.Add(entry);
            SelectedSource = entry;
        }

        public void AddTarget(string folderPath, string shortcut)
        {
            var target = new TargetFolderViewModel
            {
                FolderPath = folderPath,
                Name = new DirectoryInfo(folderPath).Name,
                Shortcut = shortcut
            };
            Targets.Add(target);
            SelectedTarget = target;
        }

        public bool TryBuildSettings(out AppSettings settings)
        {
            settings = null;
            var sourceFolders = Sources
                .Select(source => source.Path?.Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var targetFolders = Targets
                .Select(target => target.ToModel())
                .Where(target => !string.IsNullOrWhiteSpace(target.FolderPath) ||
                                 !string.IsNullOrWhiteSpace(target.Shortcut) ||
                                 !string.IsNullOrWhiteSpace(target.Name))
                .ToList();

            if (sourceFolders.Count == 0)
            {
                settings = BuildSettings(sourceFolders, targetFolders);
                ValidationMessage = "至少需要一个 source folder。";
                CurrentSettings = settings;
                return true;
            }

            if (targetFolders.Count == 0)
            {
                settings = BuildSettings(sourceFolders, targetFolders);
                ValidationMessage = "至少需要一个 target folder。";
                CurrentSettings = settings;
                return true;
            }

            if (sourceFolders.Any(path => !Directory.Exists(path)))
            {
                ValidationMessage = "存在不可访问的 source folder。";
                return false;
            }

            if (targetFolders.Any(target => string.IsNullOrWhiteSpace(target.FolderPath)))
            {
                ValidationMessage = "每个 target folder 都需要设置路径。";
                return false;
            }

            if (targetFolders.Any(target => string.IsNullOrWhiteSpace(target.Shortcut)))
            {
                ValidationMessage = "每个 target folder 都需要绑定 A-Z 或 0-9 的单个快捷键。";
                return false;
            }

            var duplicateShortcut = targetFolders
                .GroupBy(target => target.Shortcut, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateShortcut != null)
            {
                ValidationMessage = $"快捷键 {duplicateShortcut.Key} 被重复使用。";
                return false;
            }

            foreach (var target in targetFolders)
            {
                if (string.IsNullOrWhiteSpace(target.Name))
                {
                    target.Name = new DirectoryInfo(target.FolderPath).Name;
                }
            }

            settings = BuildSettings(sourceFolders, targetFolders);
            CurrentSettings = settings;
            ValidationMessage = string.Empty;
            return true;
        }

        private AppSettings BuildSettings(
            System.Collections.Generic.List<string> sourceFolders,
            System.Collections.Generic.List<TargetFolder> targetFolders)
        {
            return new AppSettings
            {
                SourceFolders = sourceFolders,
                TargetFolders = targetFolders,
                WindowWidth = CurrentSettings.WindowWidth,
                WindowHeight = CurrentSettings.WindowHeight
            };
        }
    }
}
