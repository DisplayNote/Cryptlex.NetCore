namespace Cryptlex.NetCore.Contracts
{
    public interface IPersistence
    {
        void Store(string dataKey, string value);
        string Read(string getDataKey);
    }
}