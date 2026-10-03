using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace spotthedifference
{
    public readonly record struct LevelCircle(float X, float Y, int Radius);
    public sealed record LevelData(Guid Id, string Name, int Width, int Height,
        byte[] OriginalPng, byte[] NewPng, LevelCircle[] Circles);

    /// <summary>Version 1: SPOT, version, GUID, name, canvas size, two PNG blobs, then circle records.</summary>
    public static class LevelFile
    {
        public static LevelData Load(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 160 * 1024 * 1024) throw new InvalidDataException("Level file is too large.");
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "SPOT" || reader.ReadInt32() != 1)
                throw new InvalidDataException("Unsupported level format.");
            byte[] idBytes = reader.ReadBytes(16);
            if (idBytes.Length != 16) throw new EndOfStreamException();
            Guid id = new Guid(idBytes);
            if (id == Guid.Empty) throw new InvalidDataException("Missing level ID.");
            int nameLength = reader.Read7BitEncodedInt();
            if (nameLength < 1 || nameLength > 1024) throw new InvalidDataException("Invalid level name.");
            byte[] nameBytes = reader.ReadBytes(nameLength);
            if (nameBytes.Length != nameLength) throw new EndOfStreamException();
            string name = Encoding.UTF8.GetString(nameBytes);
            int width = reader.ReadInt32(), height = reader.ReadInt32();
            if (width < 96 || height < 245 || width > 16384 || height > 16384)
                throw new InvalidDataException("Invalid level dimensions.");
            byte[] original = ReadImage(reader), modified = ReadImage(reader);
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000 || stream.Length - stream.Position != count * 12L)
                throw new InvalidDataException("Invalid circle records.");
            var circles = new LevelCircle[count];
            for (int i = 0; i < count; i++)
            {
                float x = reader.ReadSingle(), y = reader.ReadSingle();
                int radius = reader.ReadInt32();
                if (!float.IsFinite(x) || !float.IsFinite(y) || x < 0 || x > width || y < 0 || y > height
                    || radius < 1 || radius > Math.Min(width, height))
                    throw new InvalidDataException("Invalid circle position or radius.");
                circles[i] = new LevelCircle(x, y, radius);
            }
            return new LevelData(id, name, width, height, original, modified, circles);
        }

        private static byte[] ReadImage(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 8 || length > 64 * 1024 * 1024 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid embedded image length.");
            byte[] data = reader.ReadBytes(length);
            byte[] pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < pngSignature.Length; i++)
                if (data[i] != pngSignature[i]) throw new InvalidDataException("Embedded image is not a PNG.");
            return data;
        }

        public static string LevelsDirectory
        {
            get
            {
                // Development builds save into the project's levels folder.
                // Distributed builds use levels beside the executable.
                for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
                    directory != null; directory = directory.Parent)
                    if (File.Exists(Path.Combine(directory.FullName, "spotthedifference.csproj")))
                        return Path.Combine(directory.FullName, "levels");
                return Path.Combine(AppContext.BaseDirectory, "levels");
            }
        }

        public static string Save(string directory, string name, byte[] originalPng, byte[] newPng,
            int canvasWidth, int canvasHeight, IReadOnlyList<LevelCircle> circles)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 60)
                throw new ArgumentException("Enter a level name (1-60 characters).");
            if (originalPng == null || originalPng.Length == 0 || newPng == null || newPng.Length == 0)
                throw new ArgumentException("Upload both images before saving.");
            if (circles == null || circles.Count == 0)
                throw new ArgumentException("Add at least one circle before saving.");
            if (canvasWidth <= 0 || canvasHeight <= 0) throw new ArgumentException("Invalid canvas size.");
            foreach (LevelCircle circle in circles)
                if (!float.IsFinite(circle.X) || !float.IsFinite(circle.Y) || circle.Radius <= 0)
                    throw new ArgumentException("A circle contains invalid position or size data.");

            var safeName = new StringBuilder();
            foreach (char character in name)
                safeName.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0
                    || character == '/' || character == '\\' ? '_' : character);
            string stem = safeName.ToString().TrimEnd(' ', '.');
            if (stem.Length == 0) stem = "level";
            Guid id = Guid.NewGuid();
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, $"{stem}-{id:N}.spot");
            string temporary = destination + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                    writer.Write(new byte[] { 83, 80, 79, 84 });
                    writer.Write(1);
                    writer.Write(id.ToByteArray());
                    writer.Write(name);
                    writer.Write(canvasWidth);
                    writer.Write(canvasHeight);
                    writer.Write(originalPng.Length);
                    writer.Write(originalPng);
                    writer.Write(newPng.Length);
                    writer.Write(newPng);
                    writer.Write(circles.Count);
                    foreach (LevelCircle circle in circles)
                    {
                        writer.Write(circle.X);
                        writer.Write(circle.Y);
                        writer.Write(circle.Radius);
                    }
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, destination);
                return destination;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
