using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unsquare.Core.Persistence;

namespace Unsquare.Tests.Persistence
{
    public class AtomicJsonFileTests
    {
        private TempDirectory _temp;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _path = _temp.File("data.json");
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void Read_MissingFile_ReturnsMissing()
        {
            Assert.That(AtomicJsonFile.Read(_path, out List<int> value), Is.EqualTo(JsonReadStatus.Missing));
            Assert.That(value, Is.Null);
        }

        [Test]
        public void Write_ThenRead_RoundTrips()
        {
            AtomicJsonFile.Write(_path, new List<int> { 1, 2, 3 });
            Assert.That(AtomicJsonFile.Read(_path, out List<int> value), Is.EqualTo(JsonReadStatus.Ok));
            Assert.That(value, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void SecondWrite_KeepsPreviousVersionAsBackup()
        {
            AtomicJsonFile.Write(_path, new List<int> { 1 });
            AtomicJsonFile.Write(_path, new List<int> { 2 });
            Assert.That(File.ReadAllText(AtomicJsonFile.BackupPath(_path)), Does.Contain("1"));
        }

        [Test]
        public void Read_CorruptMain_FallsBackToBackup()
        {
            AtomicJsonFile.Write(_path, new List<int> { 1 });
            AtomicJsonFile.Write(_path, new List<int> { 2 });
            File.WriteAllText(_path, "{ broken");
            Assert.That(AtomicJsonFile.Read(_path, out List<int> value), Is.EqualTo(JsonReadStatus.FromBackup));
            Assert.That(value, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Read_AllCorrupt_ReturnsCorrupt()
        {
            File.WriteAllText(_path, "not json");
            Assert.That(AtomicJsonFile.Read(_path, out List<int> _), Is.EqualTo(JsonReadStatus.Corrupt));
        }

        [Test]
        public void Write_CreatesMissingDirectory()
        {
            string nested = Path.Combine(_temp.Path, "a", "b", "file.json");
            AtomicJsonFile.Write(nested, new List<int>());
            Assert.That(File.Exists(nested), Is.True);
        }
    }
}
