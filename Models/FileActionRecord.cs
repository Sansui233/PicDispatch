namespace PicDispatch.Models
{
    public class FileActionEntry
    {
        public FileActionEntry(string fromPath, string toPath, string originalPath, string sourceFolder)
        {
            FromPath = fromPath;
            ToPath = toPath;
            OriginalPath = originalPath;
            SourceFolder = sourceFolder;
        }

        public string FromPath { get; }

        public string ToPath { get; }

        public string OriginalPath { get; }

        public string SourceFolder { get; }
    }

    public enum FileActionKind
    {
        MoveToTarget,
        MoveToTrash,
        ClassifyFromTrash,
        RemoveFromTrash,
        ClearTrash
    }

    public class FileActionRecord
    {
        public FileActionRecord(
            FileActionKind kind,
            string fromPath,
            string toPath,
            string originalPath,
            string sourceFolder,
            int queueIndex)
        {
            Kind = kind;
            FromPath = fromPath;
            ToPath = toPath;
            OriginalPath = originalPath;
            SourceFolder = sourceFolder;
            QueueIndex = queueIndex;
            Entries = new[] { new FileActionEntry(fromPath, toPath, originalPath, sourceFolder) };
        }

        public FileActionRecord(FileActionKind kind, FileActionEntry[] entries)
        {
            Kind = kind;
            Entries = entries;
            FromPath = entries.Length > 0 ? entries[0].FromPath : string.Empty;
            ToPath = entries.Length > 0 ? entries[0].ToPath : string.Empty;
            OriginalPath = entries.Length > 0 ? entries[0].OriginalPath : string.Empty;
            SourceFolder = entries.Length > 0 ? entries[0].SourceFolder : string.Empty;
            QueueIndex = -1;
        }

        public FileActionKind Kind { get; }

        public FileActionEntry[] Entries { get; }

        public string FromPath { get; }

        public string ToPath { get; }

        public string OriginalPath { get; }

        public string SourceFolder { get; }

        public int QueueIndex { get; }
    }
}
