using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class SaveFileTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dicethrower_tests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            File.Delete(_dir + ".tmp"); // left behind by the failing-save test
        }

        [Test]
        public void MissingFileGivesFreshDataWithoutError()
        {
            var file = new SaveFile(_path);
            SaveData data = file.Load();
            Assert.IsNotNull(data);
            Assert.IsNull(file.LastError);
            Assert.AreEqual(0, data.Presets.Count);
        }

        [Test]
        public void SaveCreatesTheFolderAndLoadReadsItBack()
        {
            var data = new SaveData();
            data.Settings.Haptics = false;
            var file = new SaveFile(_path);

            Assert.IsTrue(file.Save(data));
            Assert.IsFalse(File.Exists(_path + ".tmp"));
            Assert.IsFalse(new SaveFile(_path).Load().Settings.Haptics);

            data.Settings.Haptics = true;
            Assert.IsTrue(file.Save(data)); // overwrite an existing file
            Assert.IsTrue(new SaveFile(_path).Load().Settings.Haptics);
        }

        [Test]
        public void CorruptFileGivesFreshDataAndIsKept()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{ this is not json");
            var file = new SaveFile(_path);

            SaveData data = file.Load();

            Assert.AreEqual(0, data.Presets.Count);
            Assert.IsInstanceOf<FormatException>(file.LastError);
            Assert.AreEqual("{ this is not json", File.ReadAllText(_path + ".corrupt"));
        }

        [Test]
        public void SaveFailureIsReportedNotThrown()
        {
            Directory.CreateDirectory(_dir);
            var file = new SaveFile(_dir); // a folder, not a file
            Assert.IsFalse(file.Save(new SaveData()));
            Assert.IsNotNull(file.LastError);
        }
    }
}
