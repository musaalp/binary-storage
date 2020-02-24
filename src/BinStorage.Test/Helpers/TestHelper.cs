using BinStorage.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace BinStorage.Test.Helpers
{
    public class TestHelper
    {
        public static void AreEqual(IndexData expected, IndexData actual)
        {
            Assert.AreEqual(expected.Size, actual.Size);
            Assert.AreEqual(expected.Offset, actual.Offset);
            Assert.IsTrue(expected.Md5Hash.SequenceEqual(actual.Md5Hash));
        }
    }
}
