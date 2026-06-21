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
        private readonly Stack<FileActionRecord> _history = new Stack<FileActionRecord>();
        private readonly List<TrashItem> _trashItems = new List<TrashItem>();
        private readonly string _trashRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PicDispatch",
            "Trash",
            Guid.NewGuid().ToString("N"));
        private readonly string _removedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PicDispatch",
            "Removed",
            Guid.NewGuid().ToString("N"));

        public event Action StateChanged;

        public bool CanUndo => _history.Count > 0;

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
            Directory.CreateDirectory(_trashRoot);
            var destinationPath = CreateUniquePath(Path.Combine(_trashRoot, Path.GetFileName(item.Path)));
            File.Move(item.Path, destinationPath);

            _trashItems.Add(new TrashItem(destinationPath, item.Path, item.SourceFolder));
            var record = new FileActionRecord(FileActionKind.MoveToTrash, item.Path, destinationPath, item.Path, item.SourceFolder, queueIndex);
            Push(record);
            return record;
        }

        public FileActionRecord ClassifyFromTrash(TrashItem item, TargetFolder target, MoveConflictResolution conflictResolution)
        {
            var destinationPath = PrepareDestination(item.OriginalPath, target.FolderPath, conflictResolution);
            File.Move(item.TrashPath, destinationPath);
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
            RemoveTrashItem(item.TrashPath);

            var record = new FileActionRecord(FileActionKind.RemoveFromTrash, item.TrashPath, destinationPath, item.OriginalPath, item.SourceFolder, -1);
            Push(record);
            return record;
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
                    break;
                case FileActionKind.ClassifyFromTrash:
                case FileActionKind.RemoveFromTrash:
                case FileActionKind.ClearTrash:
                    foreach (var entry in record.Entries)
                    {
                        _trashItems.Add(new TrashItem(entry.FromPath, entry.OriginalPath, entry.SourceFolder));
                    }

                    break;
            }
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
    }
}
