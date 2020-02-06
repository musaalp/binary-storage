namespace BinStorage
{
    public interface ITransaction
    {
        void Commit();

        void Rollback();
    }
}
