using System;
using System.IO;

namespace BinStorage.Exceptions
{
    public class NotEnoughDiskSpaceException : IOException
    {
        public NotEnoughDiskSpaceException()
        {
        }

        public NotEnoughDiskSpaceException(string message) : base(message)
        {
        }

        public NotEnoughDiskSpaceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
