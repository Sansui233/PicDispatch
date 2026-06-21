using System;
using System.Collections.Generic;
using System.IO;
using PicDispatch.Models;

namespace PicDispatch.Services
{
    public enum MoveConflictResolution
    {
        Cancel,
        AppendNumber
    }

    public class FileMoveService
    {
        private readonly Stack<MoveRecord> _history = new Stack<MoveRecord>();

        public bool CanUndo => _history.Count > 0;

        public bool DestinationExists(ImageItem item, TargetFolder target)
        {
            return File.Exists(GetDestinationPath(item, target.FolderPath));
        }

        public MoveRecord Move(ImageItem item, TargetFolder target, MoveConflictResolution conflictResolution)
        {
            Directory.CreateDirectory(target.FolderPath);
            var destinationPath = GetDestinationPath(item, target.FolderPath);
            if (File.Exists(destinationPath))
            {
                if (conflictResolution == MoveConflictResolution.Cancel)
                {
                    throw new IOException("Destination file already exists.");
                }

                destinationPath = CreateNumberedPath(destinationPath);
            }

            File.Move(item.Path, destinationPath);
            var record = new MoveRecord(item.Path, destinationPath, item.SourceFolder);
            _history.Push(record);
            return record;
        }

        public MoveRecord Undo()
        {
            if (_history.Count == 0)
            {
                return null;
            }

            var record = _history.Pop();
            if (File.Exists(record.OriginalPath))
            {
                throw new IOException("Original path already exists.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(record.OriginalPath));
            File.Move(record.MovedPath, record.OriginalPath);
            return record;
        }

        private static string GetDestinationPath(ImageItem item, string targetFolder)
        {
            return Path.Combine(targetFolder, Path.GetFileName(item.Path));
        }

        private static string CreateNumberedPath(string path)
        {
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
