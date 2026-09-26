using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace iPaint.Services
{
    public class GalleryItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long EditingTimeMs { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public string ThumbnailPath { get; set; }
    }

    /// <summary>Persists artworks (layer PNGs + metadata.json) under LocalState/Artworks.</summary>
    public static class ArtworkStore
    {
        public static string RootDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iPaint");
                return Path.Combine(dir, "Artworks");
            }
        }

        public static void EnsureRoot() => Directory.CreateDirectory(RootDir);

        public static List<GalleryItem> LoadGallery()
        {
            var list = new List<GalleryItem>();
            EnsureRoot();
            foreach (var dir in Directory.GetDirectories(RootDir).OrderByDescending(d => d))
            {
                string metaPath = Path.Combine(dir, "metadata.json");
                if (!File.Exists(metaPath)) continue;
                try
                {
                    var item = JsonConvert.DeserializeObject<GalleryItem>(File.ReadAllText(metaPath));
                    item.ThumbnailPath = Path.Combine(dir, "thumbnail.png");
                    list.Add(item);
                }
                catch { /* skip damaged entry */ }
            }
            return list.OrderByDescending(i => i.LastModified).ToList();
        }

        /// <summary>Saves the document as a new gallery entry and returns its id.</summary>
        public static async Task<string> SaveAsync(Models.ArtworkDocument doc, string existingId = null)
        {
            EnsureRoot();
            string id = existingId ?? Guid.NewGuid().ToString("N");
            string dir = Path.Combine(RootDir, id);
            Directory.CreateDirectory(dir);

            // Layers as PNGs (bottom → top order in filenames)
            int index = 0;
            foreach (var layer in doc.Layers)
            {
                string file = Path.Combine(dir, $"layer_{index:00}.png");
                await EncodePngAsync(file, layer.BgraPixels, doc.Width, doc.Height);
                index++;
            }
            // prune stale layer files
            foreach (var stale in Directory.GetFiles(dir, "layer_*.png").OrderBy(f => f).Skip(index))
                File.Delete(stale);

            var meta = new GalleryItem
            {
                Id = id,
                Title = doc.Title,
                Width = doc.Width,
                Height = doc.Height,
                EditingTimeMs = doc.EditingTimeMs,
                CreatedAt = doc.CreatedAt,
                LastModified = DateTime.Now,
            };
            File.WriteAllText(Path.Combine(dir, "metadata.json"), JsonConvert.SerializeObject(meta, Formatting.Indented));

            // Composite thumbnail
            var composited = new byte[doc.Width * doc.Height * 4];
            BrushEngine.Composite(doc.Layers, doc.Width, doc.Height, doc.BackgroundType, composited);
            await EncodePngAsync(Path.Combine(dir, "thumbnail.png"), composited, doc.Width, doc.Height);
            return id;
        }

        public static async Task<Models.ArtworkDocument> LoadAsync(GalleryItem item)
        {
            string dir = Path.Combine(RootDir, item.Id);
            var doc = new Models.ArtworkDocument
            {
                Title = item.Title,
                Width = item.Width,
                Height = item.Height,
                EditingTimeMs = item.EditingTimeMs,
                CreatedAt = item.CreatedAt,
            };
            foreach (var file in Directory.GetFiles(dir, "layer_*.png").OrderBy(f => f))
            {
                var (pixels, w, h) = await DecodePngAsync(file);
                var layer = new Models.PaintLayer(w, h, false) { BgraPixels = pixels };
                doc.Layers.Add(layer);
            }
            if (doc.Layers.Count == 0)
                doc.Layers.Add(new Models.PaintLayer(doc.Width, doc.Height, false));
            return doc;
        }

        public static void Delete(GalleryItem item)
        {
            string dir = Path.Combine(RootDir, item.Id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public static async Task EncodePngAsync(string path, byte[] bgra, int width, int height)
        {
            // The target file does not exist yet — create it, then open via WinRT for the encoder.
            using (System.IO.File.Create(path)) { }
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                (uint)width, (uint)height, 96, 96, bgra);
            await encoder.FlushAsync();
        }

        public static async Task<(byte[] pixels, int w, int h)> DecodePngAsync(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = new BitmapTransform();
            var pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight, transform, ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            return (pixelData.DetachPixelData(), (int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight);
        }

        /// <summary>Imports an image file (any decoder-supported format) into a BGRA buffer.</summary>
        public static async Task<(byte[] pixels, int w, int h)> DecodeImageAsync(string path)
            => await DecodePngAsync(path);
    }
}
