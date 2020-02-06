namespace BinStorage.Index
{
    public class Node
    {
        /// <summary>
        ///     Keeps  all information about the key.
        /// </summary>        
        public KeyInfo KeyInfo { get; set; }

        /// <summary>
        ///     Represents original data position, size and Md5 on the stream
        /// </summary>
        public IndexData IndexData { get; set; }
    }
}
