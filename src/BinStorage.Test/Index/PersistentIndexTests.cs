using BinStorage.Exceptions;
using BinStorage.Index;
using BinStorage.Test.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinStorage.Test.Index
{
    [TestClass]
    public class PersistentIndexTests : IndexTest
    {
        private string _indexFilePath;

        [TestInitialize]
        public void TestInitialize()
        {
            _indexFilePath = Path.GetTempFileName();
            File.Delete(_indexFilePath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            File.Delete(_indexFilePath);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Add_WithNullKey_ThrowArgumentNullException()
        {
            //Arrange
            var key = string.Empty;
            var indexData = new IndexData();

            //Action            
            using (var index = new PersistentIndex(_indexFilePath, IndexBinTestCapacity))
            {
                index.Add(key, indexData);
            }

            //Assert
            //Should throw ArgumentNullException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Add_WithNullIndexData_ThrowArgumentNullException()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();
            IndexData indexData = null;

            //Action            
            using (var index = new PersistentIndex(_indexFilePath, IndexBinTestCapacity))
            {
                index.Add(key, indexData);
            }

            //Assert
            //Should throw ArgumentNullException
        }

        [TestMethod]
        [ExpectedException(typeof(NotEnoughDiskSpaceException))]
        public void Add_WithOverFreeDiskSpace_ThrowNotEnoughDiskSpaceException()
        {
            //Arrange
            var bigCapacity = Sizes.Size1Tb * 10; //Assumes 10Tb will exceed any exists hard disk capacity            
            var indexData = new IndexData();
            var key = Guid.NewGuid().ToString();

            //Action
            using (var storage = new PersistentIndex(_indexFilePath, bigCapacity))
            {
                storage.Add(key, indexData);
            }

            //Assert
            //Should throw NotEnoughDiskSpaceException
        }

        [TestMethod]
        public void AddAndContains_WithSimpleData_ShouldAddCorrectlyAndContainsKey()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();
            var data = new IndexData { Offset = 1, Size = 2, Md5Hash = Guid.NewGuid().ToByteArray() };

            using (var index = Create())
            {
                //Action
                index.Add(key, data);

                //Assert
                Assert.IsTrue(index.Contains(key));
            }
        }

        [TestMethod]
        public void AddAndGet_WithCollectionData_ShouldAddAndGetCorrectly()
        {
            //Arrange
            var dictionary = Enumerable.Range(0, 100)
                .ToDictionary(
                    x => Guid.NewGuid().ToString(),
                    x => new IndexData
                    {
                        Offset = x + 1,
                        Md5Hash = Guid.NewGuid().ToByteArray(),
                        Size = x + 3
                    });

            var capacity = dictionary.Sum(p => p.Key.Length + IndexBinDefaultNodeSize);

            using (var index = Create(capacity))
            {
                foreach (var item in dictionary)
                {
                    //Action
                    index.Add(item.Key, item.Value);
                }

                foreach (var item in dictionary)
                {
                    //Action
                    var data = index.Get(item.Key);

                    //Assert
                    TestHelper.AreEqual(item.Value, data);
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Get_WithNullKey_ThrowArgumentNullException()
        {
            //Arrange
            var key = string.Empty;

            //Action            
            using (var index = Create())
            {
                var indexData = index.Get(key);
            }

            //Assert
            //Should throw ArgumentNullException
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Get_NotExistsKey_ThrowKeyNotFoundException()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();

            //Action            
            using (var index = Create())
            {
                var indexData = index.Get(key);
            }

            //Assert
            //Should throw KeyNotFoundException
        }

        [TestMethod]
        [ExpectedException(typeof(DuplicateException))]
        public void Get_DublicateKeysWithDifferentIntances_ThrowDuplicateException()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();

            var data = new IndexData
            {
                Offset = 1,
                Size = 2,
                Md5Hash = Guid.NewGuid().ToByteArray()
            };

            var newData = new IndexData
            {
                Offset = 3,
                Size = 4,
                Md5Hash = Guid.NewGuid().ToByteArray()
            };

            //Action
            //Intance1
            using (var index = Create())
            {
                index.Add(key, data);
            }

            //Action
            //Intance2
            using (var index = Create())
            {
                index.Add(key, newData);
            }

            //Assert
            //Should throw DuplicateException
        }

        [TestMethod]
        [ExpectedException(typeof(DuplicateException))]
        public void Get_DublicateKeysWithSameIntance_ThrowDuplicateException()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();

            var data = new IndexData
            {
                Offset = 1,
                Size = 2,
                Md5Hash = Guid.NewGuid().ToByteArray()
            };

            var newData = new IndexData
            {
                Offset = 3,
                Size = 4,
                Md5Hash = Guid.NewGuid().ToByteArray()
            };

            //Action
            //Same intance
            using (var index = Create())
            {
                index.Add(key, data);
                index.Add(key, newData);
            }

            //Assert
            //Should throw DuplicateException
        }

        [TestMethod]
        public void Get_WithSameAndDifferentIntances_ShouldReturnCorrectData()
        {
            var key = Guid.NewGuid().ToString();
            var data = new IndexData
            {
                Offset = 1,
                Size = 2,
                Md5Hash = Guid.NewGuid().ToByteArray()
            };

            using (var index = Create())
            {
                index.Add(key, data);

                var actual = index.Get(key);

                TestHelper.AreEqual(data, actual);
            }

            using (var index = Create())
            {
                var actual = index.Get(key);

                TestHelper.AreEqual(data, actual);
            }
        }

        [TestMethod]
        public void Commit_WithSimpleData_ShouldAddCorrectlyAndContainsKey()
        {
            //Arrange
            var key = Guid.NewGuid().ToString();
            var data = new IndexData { Offset = 1, Size = 2, Md5Hash = Guid.NewGuid().ToByteArray() };

            using (var index = Create())
            {
                //Action
                index.Add(key, data);
                index.Commit();

                var actual = index.Get(key);

                //Assert
                Assert.IsTrue(index.Contains(key));
                TestHelper.AreEqual(data, actual);
            }
        }

        [TestMethod]
        public void Rollback_WhenAnyErrorHappen_ShouldRollback()
        {
            //Arrange
            var dictionary = Enumerable.Range(0, 5)
                .ToDictionary(
                    x => Guid.NewGuid().ToString(),
                    x => new IndexData
                    {
                        Offset = x + 1,
                        Md5Hash = Guid.NewGuid().ToByteArray(),
                        Size = x + 3
                    });

            var capacity = dictionary.Sum(p => p.Key.Length + IndexBinDefaultNodeSize);

            //Action
            //add successfully
            using (var index = Create(capacity))
            {
                foreach (var item in dictionary)
                {
                    index.Add(item.Key, item.Value);
                }
            }

            //keep add, make an error and rollback
            using (var index = Create(capacity))
            {
                try
                {
                    var key = Guid.NewGuid().ToString();
                    var indexData = new IndexData
                    {
                        Offset = 1,
                        Size = 2,
                        Md5Hash = Guid.NewGuid().ToByteArray()
                    };

                    index.Add(key, indexData);
                    index.Add(null, null);
                }
                catch (Exception)
                {
                    //rollback last added item
                    index.Rollback();
                }
            }

            //check added after rollback
            using (var index = Create(capacity))
            {
                foreach (var item in dictionary)
                {
                    var data = index.Get(item.Key);

                    //Assert
                    TestHelper.AreEqual(item.Value, data);
                }
            }
        }

        protected override IIndex Create(long capacity = IndexBinTestCapacity)
        {
            return new PersistentIndex(_indexFilePath, capacity);
        }
    }
}
