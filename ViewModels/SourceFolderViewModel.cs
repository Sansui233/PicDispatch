using System.IO;

namespace PicDispatch.ViewModels
{
    public class SourceFolderViewModel : ObservableObject
    {
        private int _remainingCount;
        private bool _isSelected;

        public SourceFolderViewModel(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string Name
        {
            get
            {
                var name = System.IO.Path.GetFileName(Path.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar));
                return string.IsNullOrWhiteSpace(name) ? Path : name;
            }
        }

        public int RemainingCount
        {
            get => _remainingCount;
            set
            {
                if (SetProperty(ref _remainingCount, value))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayText => $"{Name}  {RemainingCount}";
    }
}
