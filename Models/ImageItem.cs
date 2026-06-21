namespace PicDispatch.Models
{
    public class ImageItem
    {
        public ImageItem(string path, string sourceFolder)
        {
            Path = path;
            SourceFolder = sourceFolder;
        }

        public string Path { get; }

        public string SourceFolder { get; }

        public string FileName => System.IO.Path.GetFileName(Path);
    }
}
