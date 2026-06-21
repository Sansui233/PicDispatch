using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PicDispatch.Models;

namespace PicDispatch.Services
{
    public class ImageQueueService
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".bmp",
                ".gif",
                ".tif",
                ".tiff",
                ".webp",
                ".avif"
            };

        public List<ImageItem> BuildQueue(IEnumerable<string> sourceFolders)
        {
            var result = new List<ImageItem>();
            foreach (var sourceFolder in sourceFolders.Where(Directory.Exists))
            {
                var files = Directory.EnumerateFiles(sourceFolder)
                    .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
                    .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(file => file, StringComparer.CurrentCultureIgnoreCase);

                result.AddRange(files.Select(file => new ImageItem(file, sourceFolder)));
            }

            return result;
        }
    }
}
