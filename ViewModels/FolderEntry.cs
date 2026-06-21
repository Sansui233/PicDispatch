namespace PicDispatch.ViewModels
{
    public class FolderEntry : ObservableObject
    {
        private string _path = string.Empty;

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }
    }
}
