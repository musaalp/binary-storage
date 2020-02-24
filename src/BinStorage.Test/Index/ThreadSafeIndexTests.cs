using BinStorage.Index;
using BinStorage.Test.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BinStorage.Test.Index
{
    [TestClass]
    public class ThreadSafeIndexTests : IndexTest
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
        public void AddAndGet_MultiThreadsWithSameAndDifferentIntances_ShouldReturnCorrectData()
        {
            var data = Enumerable.Range(0, 1000).ToDictionary(
                x => Guid.NewGuid().ToString(),
                i => new IndexData
                {
                    Md5Hash = Guid.NewGuid().ToByteArray(),
                    Offset = i + 1,
                    Size = i + 3
                });

            var capacity = data.Sum(p => p.Key.Length + IndexBinDefaultNodeSize);

            using (var index = Create(capacity))
            {
                Parallel.Invoke(
                    () =>
                    {
                        data.OrderBy(x => Guid.NewGuid())
                            .AsParallel()
                            .WithDegreeOfParallelism(4)
                            .ForAll(
                                item =>
                                {
                                    var retries = 3;
                                    while (retries-- > 0)
                                    {
                                        try
                                        {
                                            index.Add(item.Key, item.Value);
                                            break;
                                        }
                                        catch (TimeoutException)
                                        {
                                            if (retries == 0)
                                            {
                                                throw;
                                            }
                                        }
                                    }
                                });
                    },
                    () =>
                    {
                        data.OrderBy(x => Guid.NewGuid())
                            .AsParallel()
                            .WithDegreeOfParallelism(4)
                            .ForAll(
                                item =>
                                {
                                    try
                                    {
                                        var indexData = index.Get(item.Key);
                                        TestHelper.AreEqual(indexData, data[item.Key]);
                                    }
                                    catch (KeyNotFoundException)
                                    {
                                    }
                                    catch (TimeoutException)
                                    {
                                    }
                                });
                    });
            }

            using (var index = Create(capacity))
            {
                foreach (var item in data)
                {
                    var actual = index.Get(item.Key);
                    TestHelper.AreEqual(item.Value, actual);
                }
            }
        }

        protected override IIndex Create(long capacity = 1024)
        {
            return new ThreadSafeIndex(new PersistentIndex(_indexFilePath, capacity), TimeSpan.FromSeconds(90));
        }
    }
}
