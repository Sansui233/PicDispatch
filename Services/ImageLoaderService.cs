using System;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace PicDispatch.Services
{
    public class ImageLoaderService
    {
        public BitmapSource Load(string path)
        {
            try
            {
                return LoadWithWpf(path);
            }
            catch
            {
                return LoadWithMagick(path);
            }
        }

        public BitmapSource LoadThumbnail(string path, int size = 48)
        {
            try
            {
                return LoadThumbnailWithWpf(path, size);
            }
            catch
            {
                return LoadThumbnailWithMagick(path, size);
            }
        }

        private static BitmapSource LoadWithWpf(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static BitmapSource LoadThumbnailWithWpf(string path, int size)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = size;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static BitmapSource LoadWithMagick(string path)
        {
            using (var image = new MagickImage(path))
            using (var stream = new MemoryStream())
            {
                image.AutoOrient();
                image.Format = MagickFormat.Png;
                image.Write(stream);
                stream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static BitmapSource LoadThumbnailWithMagick(string path, int size)
        {
            using (var image = new MagickImage(path))
            using (var stream = new MemoryStream())
            {
                image.AutoOrient();
                image.Thumbnail((uint)size, (uint)size);
                image.Format = MagickFormat.Png;
                image.Write(stream);
                stream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
    }
}
