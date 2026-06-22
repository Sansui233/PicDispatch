using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace PicDispatch.Models
{
    public class TrashItem : INotifyPropertyChanged
    {
        private BitmapSource _thumbnail;

        public TrashItem(string trashPath, string originalPath, string sourceFolder)
        {
            TrashPath = trashPath;
            OriginalPath = originalPath;
            SourceFolder = sourceFolder;
        }

        public string TrashPath { get; }

        public string OriginalPath { get; }

        public string SourceFolder { get; }

        public string FileName => Path.GetFileName(OriginalPath);

        public BitmapSource Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
