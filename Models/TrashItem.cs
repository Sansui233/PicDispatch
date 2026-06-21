using System.IO;

namespace PicDispatch.Models
{
    public class TrashItem
    {
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
    }
}
