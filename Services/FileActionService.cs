using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PicDispatch.Models;

namespace PicDispatch.Services
{
    public enum MoveConflictResolution
    {
        Cancel,
        AppendNumber
    }

    public class FileActionService
    {
        private const string TrashMetadataSuffix = ".trash.xml";
        private readonly Stack<FileActionRecord> _history = new Stack<FileActionRecord>();
        private readonly List<TrashItem> _trashItems = new List<TrashItem>();
        private readonly string _trashRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PicDispatch",
            "Trash");
        private readonly string _removedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PicDispatch",
            "Removed",
            Guid.NewGuid().ToString("N"));

        public event Action StateChanged;

        public FileActionService()
        {
            LoadTrashItems();
        }

        public bool CanUndo => _history.Count > 0;

        public int UndoCount => _history.Count;

        public int TrashCount => _trashItems.Count;

        public IReadOnlyList<TrashItem> TrashItems => _trashItems;

        public bool DestinationExists(ImageItem item, TargetFolder target)
        {
            return File.Exists(GetDestinationPath(item.Path, target.FolderPath));
        }

        public bool DestinationExists(TrashItem item, TargetFolder target)
        {
            return File.Exists(GetDestinationPath(item.OriginalPath, target.FolderPath));
        }

        public FileActionRecord MoveToTarget(ImageItem item, TargetFolder target, MoveConflictResolution conflictResolution, int queueIndex)
        {
            var destinationPath = PrepareDestination(item.Path, target.FolderPath, conflictResolution);
            File.Move(item.Path, destinationPath);

            var record = new FileActionRecord(FileActionKind.MoveToTarget, item.Path, destinationPath, item.Path, item.SourceFolder, queueIndex);
            Push(record);
            return record;
        }

        public FileActionRecord MoveToTrash(ImageItem item, int queueIndex)
        {
            if (!File.Exists(item.Path))
            {
                throw new FileNotFoundException("Source file was not found.", item.Path);
            }

            Directory.CreateDirectory(_trashRoot);
            var destinationPath = CreateUniquePath(Path.Combine(_trashRoot, Path.GetFileName(item.Path)));
            File.Move(item.Path, destinationPath);

            var trashItem = new TrashItem(destinationPath, item.Path, item.SourceFolder);
            try
            {
                SaveTrashItem(trashItem);
                _trashItems.Add(trashItem);
                var record = new FileActionRecord(FileActionKind.MoveToTrash, item.Path, destinationPath, item.Path, item.SourceFolder, queueIndex);
                Push(record);
                return record;
            }
            catch
            {
                if (File.Exists(destinationPath) && !File.Exists(item.Path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Path));
                    File.Move(destinationPath, item.Path);
                }

                RemoveTrashMetadata(destinationPath);
                throw;
            }
        }

        public FileActionRecord ClassifyFromTrash(TrashItem item, TargetFolder target, MoveConflictResolution conflictResolution)
        {
            var destinationPath = PrepareDestination(item.OriginalPath, target.FolderPath, conflictResolution);
            File.Move(item.TrashPath, destinationPath);
            RemoveTrashMetadata(item.TrashPath);
            RemoveTrashItem(item.TrashPath);

            var record = new FileActionRecord(FileActionKind.ClassifyFromTrash, item.TrashPath, destinationPath, item.OriginalPath, item.SourceFolder, -1);
            Push(record);
            return record;
        }

        public FileActionRecord RemoveFromTrash(TrashItem item)
        {
            Directory.CreateDirectory(_removedRoot);
            var destinationPath = CreateUniquePath(Path.Combine(_removedRoot, Path.GetFileName(item.TrashPath)));
            File.Move(item.TrashPath, destinationPath);
            RemoveTrashMetadata(item.TrashPath);
            RemoveTrashItem(item.TrashPath);

            var record = new FileActionRecord(FileActionKind.RemoveFromTrash, item.TrashPath, destinationPath, item.OriginalPath, item.SourceFolder, -1);
            Push(record);
            return record;
        }

        public void RestoreFromTrash(TrashItem item)
        {
            if (item == null)
            {
                return;
            }

            var destinationDirectory = Path.GetDirectoryName(item.OriginalPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new IOException("Original path is invalid.");
            }

            Directory.CreateDirectory(destinationDirectory);
            var destinationPath = item.OriginalPath;
            if (File.Exists(destinationPath))
            {
                destinationPath = CreateUniquePath(destinationPath);
            }

            File.Move(item.TrashPath, destinationPath);
            RemoveTrashMetadata(item.TrashPath);
            RemoveTrashItem(item.TrashPath);
            NotifyStateChanged();
        }

        public FileActionRecord ClearTrash()
        {
            if (_trashItems.Count == 0)
            {
                return null;
            }

            Directory.CreateDirectory(_removedRoot);
            var entries = new List<FileActionEntry>();
            foreach (var item in _trashItems.ToList())
            {
                var destinationPath = CreateUniquePath(Path.Combine(_removedRoot, Path.GetFileName(item.TrashPath)));
                File.Move(item.TrashPath, destinationPath);
                RemoveTrashMetadata(item.TrashPath);
                entries.Add(new FileActionEntry(item.TrashPath, destinationPath, item.OriginalPath, item.SourceFolder));
                RemoveTrashItem(item.TrashPath);
            }

            var record = new FileActionRecord(FileActionKind.ClearTrash, entries.ToArray());
            Push(record);
            return record;
        }

        public FileActionRecord Undo()
        {
            if (_history.Count == 0)
            {
                return null;
            }

            var record = _history.Peek();
            foreach (var entry in record.Entries)
            {
                if (File.Exists(entry.FromPath))
                {
                    throw new IOException("Undo destination already exists.");
                }
            }

            foreach (var entry in record.Entries)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.FromPath));
                File.Move(entry.ToPath, entry.FromPath);
            }

            ApplyUndoState(record);
            _history.Pop();
            NotifyStateChanged();
            return record;
        }

        public bool PruneMissingTrashItems()
        {
            var removedAny = false;

            foreach (var item in _trashItems.ToList())
            {
                if (File.Exists(item.TrashPath))
                {
                    continue;
                }

                RemoveTrashMetadata(item.TrashPath);
                _trashItems.Remove(item);
                removedAny = true;
            }

            return removedAny;
        }

        private string PrepareDestination(string sourcePath, string targetFolder, MoveConflictResolution conflictResolution)
        {
            Directory.CreateDirectory(targetFolder);
            var destinationPath = GetDestinationPath(sourcePath, targetFolder);
            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }

            if (conflictResolution == MoveConflictResolution.Cancel)
            {
                throw new IOException("Destination file already exists.");
            }

            return CreateUniquePath(destinationPath);
        }

        private void ApplyUndoState(FileActionRecord record)
        {
            switch (record.Kind)
            {
                case FileActionKind.MoveToTrash:
                    RemoveTrashItem(record.ToPath);
                    RemoveTrashMetadata(record.ToPath);
                    break;
                case FileActionKind.ClassifyFromTrash:
                case FileActionKind.RemoveFromTrash:
                case FileActionKind.ClearTrash:
                    foreach (var entry in record.Entries)
                    {
                        var restoredItem = new TrashItem(entry.FromPath, entry.OriginalPath, entry.SourceFolder);
                        _trashItems.Add(restoredItem);
                        SaveTrashItem(restoredItem);
                    }

                    break;
            }
        }

        private void LoadTrashItems()
        {
            Directory.CreateDirectory(_trashRoot);

            foreach (var filePath in Directory.GetFiles(_trashRoot))
            {
                if (filePath.EndsWith(TrashMetadataSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var trashItem = LoadTrashItem(filePath);
                if (trashItem != null)
                {
                    _trashItems.Add(trashItem);
                }
            }
        }

        private static TrashItem LoadTrashItem(string trashPath)
        {
            var metadataPath = GetTrashMetadataPath(trashPath);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                using (var stream = File.OpenRead(metadataPath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TrashItemMetadata));
                    var metadata = serializer.Deserialize(stream) as TrashItemMetadata;
                    if (metadata == null)
                    {
                        return null;
                    }

                    return new TrashItem(trashPath, metadata.OriginalPath ?? trashPath, metadata.SourceFolder ?? string.Empty);
                }
            }
            catch
            {
                return null;
            }
        }

        private static void SaveTrashItem(TrashItem item)
        {
            var metadataPath = GetTrashMetadataPath(item.TrashPath);
            var metadata = new TrashItemMetadata
            {
                OriginalPath = item.OriginalPath,
                SourceFolder = item.SourceFolder
            };

            using (var stream = File.Create(metadataPath))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TrashItemMetadata));
                serializer.Serialize(stream, metadata);
            }
        }

        private static void RemoveTrashMetadata(string trashPath)
        {
            var metadataPath = GetTrashMetadataPath(trashPath);
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
        }

        private static string GetTrashMetadataPath(string trashPath)
        {
            return trashPath + TrashMetadataSuffix;
        }

        private void Push(FileActionRecord record)
        {
            _history.Push(record);
            NotifyStateChanged();
        }

        private void RemoveTrashItem(string trashPath)
        {
            var item = _trashItems.FirstOrDefault(candidate =>
                string.Equals(candidate.TrashPath, trashPath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                _trashItems.Remove(item);
            }
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string GetDestinationPath(string sourcePath, string targetFolder)
        {
            return Path.Combine(targetFolder, Path.GetFileName(sourcePath));
        }

        private static string CreateUniquePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            for (var i = 1; i < 10000; i++)
            {
                var candidate = Path.Combine(directory, $"{fileName} ({i}){extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("Could not create a unique destination path.");
        }

        public class TrashItemMetadata
        {
            public string OriginalPath { get; set; }

            public string SourceFolder { get; set; }
        }
    }
}
