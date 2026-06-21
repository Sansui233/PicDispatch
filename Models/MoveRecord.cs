namespace PicDispatch.Models
{
    public class MoveRecord
    {
        public MoveRecord(string originalPath, string movedPath, string sourceFolder)
        {
            OriginalPath = originalPath;
            MovedPath = movedPath;
            SourceFolder = sourceFolder;
        }

        public string OriginalPath { get; }

        public string MovedPath { get; }

        public string SourceFolder { get; }
    }
}
