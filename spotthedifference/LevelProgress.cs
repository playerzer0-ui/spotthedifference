using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace spotthedifference
{
    public enum LevelStatus { New, Ongoing, Completed }
    public sealed class LevelProgress
    {
        public LevelStatus Status { get; set; }
        public HashSet<int> Found { get; } = new HashSet<int>();
        public double ElapsedSeconds { get; set; }
        public int WrongClicks { get; set; }
        public bool HasStatistics { get; set; } = true;
    }

    /// <summary>All player progress in one binary file, with entries keyed by level GUID.</summary>
    public sealed class ProgressStore
    {
        private readonly string directory;
        public string FilePath => Path.Combine(directory, "player.progress");

        public ProgressStore(string directory = null)
        {
            this.directory = directory ?? LevelFile.LevelsDirectory;
        }

        /// <summary>Migrate old per-level files only after writing the combined file successfully.</summary>
        public void Initialize()
        {
            Directory.CreateDirectory(directory);
            var records = ReadRecords();
            var migrated = new List<string>();
            foreach (string path in Directory.EnumerateFiles(directory, "*.progress"))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id)) continue;
                using var reader = new BinaryReader(File.OpenRead(path));
                if (reader.ReadInt32() != 0x50544F53) throw new InvalidDataException("Invalid legacy progress file.");
                int version = reader.ReadInt32();
                if (version != 1 && version != 2) throw new InvalidDataException("Unsupported legacy progress version.");
                LevelProgress entry = ReadEntry(reader, version >= 2);
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Invalid legacy progress data.");
                if (!records.ContainsKey(id)) records.Add(id, entry);
                migrated.Add(path);
            }
            if (!File.Exists(FilePath) || migrated.Count > 0) WriteRecords(records);
            foreach (string path in migrated) File.Delete(path);
        }

        public LevelProgress Load(Guid id, int circleCount)
        {
            try
            {
                Initialize();
                var records = ReadRecords();
                if (!records.TryGetValue(id, out LevelProgress result)) return new LevelProgress();
                if (result.Found.Any(index => index >= circleCount)
                    || (result.Status == LevelStatus.Completed && result.Found.Count != circleCount))
                    throw new InvalidDataException("Progress does not match this level.");
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                return new LevelProgress();
            }
        }

        public void Save(Guid id, LevelProgress progress)
        {
            Initialize();
            Validate(progress);
            var records = ReadRecords();
            if (progress.Status == LevelStatus.New) records.Remove(id);
            else records[id] = progress;
            WriteRecords(records);
        }

        private Dictionary<Guid, LevelProgress> ReadRecords()
        {
            var result = new Dictionary<Guid, LevelProgress>();
            if (!File.Exists(FilePath)) return result;
            using var reader = new BinaryReader(File.OpenRead(FilePath));
            if (reader.BaseStream.Length > 16 * 1024 * 1024 || reader.ReadInt32() != 0x50475053 || reader.ReadInt32() != 1)
                throw new InvalidDataException("Invalid player progress file.");
            int count = reader.ReadInt32();
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid level count.");
            for (int i = 0; i < count; i++)
            {
                byte[] bytes = reader.ReadBytes(16);
                if (bytes.Length != 16) throw new EndOfStreamException();
                Guid id = new Guid(bytes);
                if (id == Guid.Empty || !result.TryAdd(id, ReadEntry(reader, true)))
                    throw new InvalidDataException("Invalid or duplicate level ID.");
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected progress data.");
            return result;
        }

        private static LevelProgress ReadEntry(BinaryReader reader, bool hasStatistics)
        {
            var entry = new LevelProgress { Status = (LevelStatus)reader.ReadByte(), HasStatistics = hasStatistics };
            if (hasStatistics)
            {
                entry.HasStatistics = reader.ReadBoolean();
                entry.ElapsedSeconds = reader.ReadDouble();
                entry.WrongClicks = reader.ReadInt32();
            }
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000 || count * 4L > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid found-marker count.");
            for (int i = 0; i < count; i++)
                if (!entry.Found.Add(reader.ReadInt32())) throw new InvalidDataException("Duplicate marker.");
            Validate(entry);
            if (entry.Status == LevelStatus.New) throw new InvalidDataException("Unexpected unplayed entry.");
            return entry;
        }

        private static void Validate(LevelProgress entry)
        {
            if (!Enum.IsDefined(entry.Status) || !double.IsFinite(entry.ElapsedSeconds) || entry.ElapsedSeconds < 0
                || entry.WrongClicks < 0 || entry.Found.Count > 10000 || entry.Found.Any(index => index < 0 || index >= 10000))
                throw new InvalidDataException("Invalid player progress values.");
        }

        private void WriteRecords(Dictionary<Guid, LevelProgress> records)
        {
            Directory.CreateDirectory(directory);
            string temporary = FilePath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                    writer.Write(0x50475053);
                    writer.Write(1);
                    writer.Write(records.Count);
                    foreach (var record in records.OrderBy(record => record.Key))
                    {
                        writer.Write(record.Key.ToByteArray());
                        LevelProgress entry = record.Value;
                        writer.Write((byte)entry.Status);
                        writer.Write(entry.HasStatistics);
                        writer.Write(entry.ElapsedSeconds);
                        writer.Write(entry.WrongClicks);
                        writer.Write(entry.Found.Count);
                        foreach (int index in entry.Found.OrderBy(index => index)) writer.Write(index);
                    }
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, FilePath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
