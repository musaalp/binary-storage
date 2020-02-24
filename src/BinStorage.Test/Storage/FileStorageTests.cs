using BinStorage.Exceptions;
using BinStorage.Index;
using BinStorage.Storage;
using BinStorage.Test.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace BinStorage.Test.Storage
{
    [TestClass]
    public class FileStorageTests
    {
        private const int StorageBinTestCapacity = Sizes.Size1Kb;
        private const int SizeOfGuid = 16;
        private string _storageFilePath;

        [TestInitialize]
        public void TestInitialize()
        {
            _storageFilePath = Path.GetTempFileName();
            File.Delete(_storageFilePath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            File.Delete(_storageFilePath);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Append_WithNullStream_ThrowArgumentNullException()
        {
            //Arrange
            using (var storage = new FileStorage(_storageFilePath, StorageBinTestCapacity))
            {
                FileStream fs = null;

                //Action            
                storage.Append(fs);
            }

            //Assert
            //Should throw ArgumentNullException
        }

        [TestMethod]
        [ExpectedException(typeof(NotEnoughDiskSpaceException))]
        public void Append_WithOverFreeDiskSpace_ThrowNotEnoughDiskSpaceException()
        {
            //Arrange
            var bigCapacity = Sizes.Size1Tb * 10; //Assumes 10Tb will exceed any exists hard disk capacity
            var path = string.Empty;

            //Action
            using (var storage = new FileStorage(_storageFilePath, bigCapacity))
            {
                path = Path.GetTempFileName();

                using (var stream = new FileStream(path, FileMode.Open))
                {
                    storage.Append(stream);
                }
            }

            //Assert
            //Should throw NotEnoughDiskSpaceException

            if (string.IsNullOrEmpty(path))
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void AppendAndGet_WithEmptyData_ShouldReturnEmptData()
        {
            //Arrange
            using (var storage = new FileStorage(_storageFilePath, StorageBinTestCapacity))
            {
                //Action
                var indexData = storage.Append(new MemoryStream());
                using (var resultStream = storage.Get(indexData))
                {
                    //Assert
                    Assert.AreEqual(0, resultStream.Length);
                }
            }

            //Assert
            Assert.AreEqual(StorageBinTestCapacity + DefaultSizes.CursorHolderSize, new FileInfo(_storageFilePath).Length);
        }

        [TestMethod]
        public void AppendAndGet_WithSimpleData_ShouldAppendAndGetCorrectly()
        {
            //Arrange            
            var data = Guid.NewGuid().ToByteArray();
            var capacity = data.Length;

            //Action
            IndexData indexData;
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                indexData = storage.Append(new MemoryStream(data));
            }

            //Assert
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                using (var resultStream = storage.Get(indexData))
                {
                    byte[] hash1, hash2;
                    Md5Helper.ComputeHashes(data, resultStream, out hash1, out hash2);

                    Assert.IsTrue(hash1.SequenceEqual(hash2));
                }
            }
        }

        [TestMethod]
        public void AppendAndGet_WithBigData_ShouldAppendAndGetCorrectly()
        {
            //Arrange            
            const long step = Int32.MaxValue / 100;
            var buffer = new byte[step];
            for (var i = 0; i < buffer.Length; i++)
            {
                var value = (byte)(i % 2 == 0 ? 0 : 1);
                buffer[i] = value;
            }

            var capacity = buffer.Length;

            //Action
            IndexData indexData;
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                indexData = storage.Append(new MemoryStream(buffer));
            }

            //Assert
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                using (var resultStream = storage.Get(indexData))
                {
                    byte[] hash1, hash2;
                    Md5Helper.ComputeHashes(buffer, resultStream, out hash1, out hash2);

                    Assert.IsTrue(hash1.SequenceEqual(hash2));
                }
            }
        }

        [TestMethod]
        public void Append_WithOverCapacity_IncreaseMemoryMappedFileCapacity()
        {
            //Arrange
            var datas = new[] { Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray() };
            var capacity = datas.Sum(d => d.Length) - 16;

            //Action
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                foreach (var data in datas)
                {
                    storage.Append(new MemoryStream(data));
                }
            }

            //Assert
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                var offset = DefaultSizes.CursorHolderSize;
                foreach (var data in datas)
                {
                    using (var resultStream = storage.Get(new IndexData
                    {
                        Offset = offset,
                        Md5Hash = null,
                        Size = data.Length
                    }))
                    {
                        byte[] hash1, hash2;
                        Md5Helper.ComputeHashes(data, resultStream, out hash1, out hash2);

                        Assert.IsTrue(hash1.SequenceEqual(hash2));
                    }
                    offset += SizeOfGuid;
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Get_WithNullIndexData_ThrowArgumentNullException()
        {
            //Arrange
            IndexData indexdata = null;

            //Action            
            using (var storage = new FileStorage(_storageFilePath, StorageBinTestCapacity))
            {
                storage.Get(indexdata);
            }

            //Assert
            //Should throw ArgumentNullException
        }

        [TestMethod]
        public void Get_WithIndexDataSizeZero_ShouldReturnStreamNull()
        {
            //Arrange
            var indexdata = new IndexData
            {
                Size = 0
            };

            //Action            
            using (var storage = new FileStorage(_storageFilePath, StorageBinTestCapacity))
            {
                var actual = storage.Get(indexdata);

                //Assert
                Assert.IsInstanceOfType(actual, typeof(Stream));
                Assert.AreEqual(0, actual.Length);
                Assert.AreEqual(0, actual.Position);
            }
        }

        [TestMethod]
        public void Get_AfterResized_ShouldGetAppendedData()
        {
            //Arrange
            var datas = new[] { Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray() };
            var capacity = datas.Sum(d => d.Length) - 16;

            //Action
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                var indexData = storage.Append(new MemoryStream(datas[0]));
                using (var resultStream = storage.Get(indexData))
                {
                    storage.Append(new MemoryStream(datas[1]));

                    byte[] hash1, hash2;
                    Md5Helper.ComputeHashes(datas[0], resultStream, out hash1, out hash2);

                    //Assert
                    Assert.IsTrue(hash1.SequenceEqual(hash2));
                }
            }
        }

        [TestMethod]
        public void Commit_WithSimpleData_ShouldWorkCorrectly()
        {
            //Arrange            
            var data = Guid.NewGuid().ToByteArray();
            var capacity = data.Length;

            //Action
            IndexData indexData;
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                indexData = storage.Append(new MemoryStream(data));
                storage.Commit();
            }

            //Assert
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                using (var resultStream = storage.Get(indexData))
                {
                    byte[] hash1, hash2;
                    Md5Helper.ComputeHashes(data, resultStream, out hash1, out hash2);

                    Assert.IsTrue(hash1.SequenceEqual(hash2));
                }
            }
        }

        [TestMethod]
        public void Rollback_WhenAnyErrorHappen_ShouldRollback()
        {
            //Arrange            
            var data = Guid.NewGuid().ToByteArray();
            var capacity = data.Length;

            //Action
            IndexData indexData;
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                indexData = storage.Append(new MemoryStream(data));
                try
                {
                    storage.Append(null);
                }
                catch (Exception)
                {
                    storage.Rollback();
                }
            }

            //Assert
            using (var storage = new FileStorage(_storageFilePath, capacity))
            {
                using (var resultStream = storage.Get(indexData))
                {
                    byte[] hash1, hash2;
                    Md5Helper.ComputeHashes(data, resultStream, out hash1, out hash2);

                    Assert.IsTrue(hash1.SequenceEqual(hash2));
                }
            }
        }
    }
}
