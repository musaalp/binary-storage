using BinStorage.Exceptions;
using BinStorage.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace BinStorage.Index
{
    public class PersistentIndex : IIndex
    {
        private readonly string _indexFilePath;

        private long _capacity;
        private long _indexCursor;
        private long _cursorPositionBeforeAction;
        private MemoryMappedFile _indexFile;
        private RootNode _rootNode;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistentIndex"/> class.
        /// </summary>
        /// <param name="indexFilePath">Physical index storage file path.</param>
        /// <param name="capacity">
        ///     The capacity of physical index storage file on the harddisk.
        ///     Capacity automatically increased to file size, or twice on overflow
        /// </param>
        public PersistentIndex(string indexFilePath, long capacity = DefaultSizes.DefaultIndexBinCapacity)
        {
            _indexFilePath = indexFilePath;
            _capacity = capacity + DefaultSizes.CursorHolderSize;

            InitFile();
        }

        public void Add(string key, IndexData indexData)
        {
            if (Contains(key))
            {
                throw new DuplicateException(nameof(key));
            }

            NewNode(key, indexData);
        }

        public bool Contains(string key)
        {
            return _rootNode.Nodes.Any(node => node.Key == key);
        }

        public IndexData Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            var keyInfo = new KeyInfo();

            try
            {
                keyInfo = _rootNode.Nodes[key].KeyInfo; // faster than linq query
            }
            catch (KeyNotFoundException ex)
            {
                throw ex;
            }

            return ReadIndexData(keyInfo);
        }

        //generates new node on the stream and adds this into rootNode.
        private void NewNode(string key, IndexData indexData)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (indexData == null)
            {
                throw new ArgumentNullException(nameof(indexData));
            }

            CheckDisposed();

            var keyBuffer = Encoding.UTF8.GetBytes(key);

            var newNodeSize = DefaultSizes.DefaultNodeSize + keyBuffer.Length;

            EnsureCapacity(_indexCursor, newNodeSize);

            using (var writer = _indexFile.CreateViewStream(_indexCursor, newNodeSize, MemoryMappedFileAccess.Write))
            {
                var keyOffset = _indexCursor + DefaultSizes.DefaultNodeSize;

                writer.WriteLong(keyOffset); // keyOffset
                writer.WriteInt(keyBuffer.Length); //keySize
                WriteIndexData(writer, indexData); //indexData
                writer.Write(keyBuffer, 0, keyBuffer.Length); //key itself

                _indexCursor += writer.Position;

                var keyInfo = new KeyInfo
                {
                    Key = key,
                    Offset = keyOffset,
                    Size = keyBuffer.Length
                };

                var node = new Node()
                {
                    KeyInfo = keyInfo,
                    IndexData = indexData,
                };

                //since we have to check if key exist before add new node, this approach accelerate check speed                 
                _rootNode.Nodes.Add(key, node);
            }

            Commit();
        }

        private static void WriteIndexData(Stream writer, IndexData data)
        {
            writer.WriteLong(data.Size);
            writer.WriteLong(data.Offset);
            writer.Write(data.Md5Hash, 0, DefaultSizes.Md5HashSize);
        }

        private void EnsureCapacity(long cursor, int length)
        {
            var required = cursor + length;
            if (required <= _capacity)
            {
                return;
            }

            //New file may need to be created with new capacity, for this reason exist _file must be released because previous process is using it. 
            ReleaseFile();

            _capacity <<= 1;
            if (required > _capacity)
            {
                _capacity = required;
            }

            InitFile();
        }

        private void CheckSpace()
        {
            if (GetFreeSpace() >= _capacity)
            {
                return;
            }

            throw new NotEnoughDiskSpaceException("There is not enough space on the disk.");
        }

        private long GetFreeSpace()
        {
            var pathRoot = Path.GetPathRoot(_indexFilePath);

            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && string.Equals(drive.Name, pathRoot, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.TotalFreeSpace)
                .First();
        }

        private void ReleaseFile(bool disposeFile = true)
        {
            using (var writer = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                writer.Write(DefaultSizes.CursorHolderOffset, _indexCursor);
            }

            if (disposeFile)
            {
                _indexFile.Dispose();
            }
        }

        //generate file with specified path and default capacity on the harddisk
        private void InitFile()
        {
            CheckSpace();

            if (File.Exists(_indexFilePath))
            {
                var length = new FileInfo(_indexFilePath).Length;
                if (_capacity < length)
                {
                    _capacity = length;
                }
            }

            _indexFile = MemoryMappedFile.CreateFromFile(
                _indexFilePath,
                FileMode.OpenOrCreate,
                null,
                _capacity,
                MemoryMappedFileAccess.ReadWrite);

            InitRootAndCursor();
        }

        //reads all node
        private void ReadRootNode()
        {
            var actualRootNode = new RootNode();

            var offset = DefaultSizes.CursorHolderSize; // start read from cursor position
            var size = _capacity - offset; // keep read until end of it

            using (var reader = _indexFile.CreateViewStream(offset, size, MemoryMappedFileAccess.Read))
            {
                while (true)
                {
                    var keyOffset = reader.ReadLong();

                    if (keyOffset == 0) // it means there are no more node to read
                    {
                        break;
                    }

                    var keySize = reader.ReadInt();
                    var indexData = ReadIndexData(new KeyInfo { Offset = keyOffset, Size = keySize });
                    var key = FetchKey(new KeyInfo { Offset = keyOffset, Size = keySize });

                    reader.Position += DefaultSizes.IndexDataSize + key.Length; //jump to the next node's keyOffset

                    var keyInfo = new KeyInfo
                    {
                        Key = key,
                        Offset = keyOffset,
                        Size = keySize
                    };

                    var node = new Node()
                    {
                        KeyInfo = keyInfo,
                        IndexData = indexData,
                    };

                    actualRootNode.Nodes.Add(key, node);
                }
            }

            _rootNode = actualRootNode;
        }

        private string FetchKey(KeyInfo keyInfo)
        {
            using (var reader = _indexFile.CreateViewAccessor(
                keyInfo.Offset,
                keyInfo.Size,
                MemoryMappedFileAccess.Read))
            {
                return ReadKey(reader, keyInfo.Size);
            }
        }

        private static string ReadKey(UnmanagedMemoryAccessor reader, int size)
        {
            var buffer = new byte[size];
            reader.ReadArray(0, buffer, 0, size);

            return Encoding.UTF8.GetString(buffer);
        }

        private IndexData ReadIndexData(KeyInfo keyInfo)
        {
            var offset = keyInfo.Offset - DefaultSizes.IndexDataSize; // Index data is stored before key
            using (var reader = _indexFile.CreateViewStream(offset, DefaultSizes.IndexDataSize, MemoryMappedFileAccess.Read))
            {
                var indexData = new IndexData
                {
                    Md5Hash = new byte[DefaultSizes.Md5HashSize],
                    Size = reader.ReadLong(),
                    Offset = reader.ReadLong()
                };
                reader.Read(indexData.Md5Hash, 0, DefaultSizes.Md5HashSize);

                return indexData;
            }
        }

        private void InitRootAndCursor()
        {
            using (var reader = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Read))
            {
                _indexCursor = reader.ReadInt64(0);
                if (_indexCursor == 0)
                {
                    _indexCursor = DefaultSizes.CursorHolderSize;

                    _rootNode = new RootNode();
                }
                else
                {
                    ReadRootNode();
                }
                _cursorPositionBeforeAction = _indexCursor;
            }
        }

        public void Commit()
        {
            //update _cursor position
            using (var writer = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                writer.Write(DefaultSizes.CursorHolderOffset, _indexCursor);
            }
        }

        public void Rollback()
        {
            //update cursor position value with previous value
            using (var cursorHolder = _indexFile.CreateViewAccessor(0, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                cursorHolder.Write(0, _cursorPositionBeforeAction);
            }

            //flush the stream from last successfully write operation to end of the file
            var size = _capacity - _cursorPositionBeforeAction;
            using (var writer = _indexFile.CreateViewStream(_cursorPositionBeforeAction, size, MemoryMappedFileAccess.Write))
            {
                writer.Flush();
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _indexFile?.Dispose();

            _disposed = true;
        }

        private bool _disposed;

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Index storage is disposed");
            }
        }

        ~PersistentIndex()
        {
            Dispose(false);
        }

        #endregion
    }
}
