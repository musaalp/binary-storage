using System;

namespace BinStorage.Index
{
    [Serializable]
    public class KeyInfo
    {
        /// <summary>
        ///     Length of key
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        ///     Position of key in index file
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        ///     Key itself, data used for processing
        /// </summary>
        public string Key { get; set; }
    }
}
