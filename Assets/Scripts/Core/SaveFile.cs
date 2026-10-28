using System;
using System.IO;

namespace DiceThrower.Core
{
    /// <summary>
    /// One save file on disk. Neither method throws for file problems: Load gives fresh data
    /// (keeping an unreadable file next to it as ".corrupt"), Save returns false. LastError says why.
    /// </summary>
    public sealed class SaveFile
    {
        private const string TempSuffix = ".tmp";
        private const string CorruptSuffix = ".corrupt";

        private readonly string _path;

        public SaveFile(string path) { _path = path; }

        public string FilePath { get { return _path; } }

        /// <summary>The problem of the last Load or Save, or null.</summary>
        public Exception LastError { get; private set; }

        public SaveData Load()
        {
            LastError = null;
            if (!File.Exists(_path)) return new SaveData();
            try
            {
                return SaveCodec.Deserialize(File.ReadAllText(_path));
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException)
            {
                LastError = e;
                KeepCorruptCopy();
                return new SaveData();
            }
        }

        /// <summary>Writes a temp file first and swaps it in, so a crash mid-write cannot damage the old save.</summary>
        public bool Save(SaveData data)
        {
            LastError = null;
            string temp = _path + TempSuffix;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(temp, SaveCodec.Serialize(data));
                if (File.Exists(_path)) File.Replace(temp, _path, null);
                else File.Move(temp, _path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                LastError = e;
                return false;
            }
        }

        private void KeepCorruptCopy()
        {
            try { File.Copy(_path, _path + CorruptSuffix, true); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }
    }
}
