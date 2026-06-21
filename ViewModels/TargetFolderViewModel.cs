using PicDispatch.Models;

namespace PicDispatch.ViewModels
{
    public class TargetFolderViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _folderPath = string.Empty;
        private string _shortcut = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FolderPath
        {
            get => _folderPath;
            set => SetProperty(ref _folderPath, value);
        }

        public string Shortcut
        {
            get => _shortcut;
            set
            {
                if (SetProperty(ref _shortcut, ShortcutNormalizer.NormalizeText(value)))
                {
                    OnPropertyChanged(nameof(DisplayShortcut));
                }
            }
        }

        public string DisplayShortcut => string.IsNullOrWhiteSpace(Shortcut) ? "Set" : Shortcut;

        public TargetFolder ToModel()
        {
            return new TargetFolder
            {
                Name = Name,
                FolderPath = FolderPath,
                Shortcut = Shortcut
            };
        }

        public static TargetFolderViewModel FromModel(TargetFolder target)
        {
            return new TargetFolderViewModel
            {
                Name = target.Name,
                FolderPath = target.FolderPath,
                Shortcut = target.Shortcut
            };
        }
    }
}
