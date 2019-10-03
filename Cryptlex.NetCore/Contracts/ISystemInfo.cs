namespace Cryptlex.NetCore.Contracts
{
    public interface ISystemInfo
    {
        string GetFingerPrint();

        string GetOsName();

        string GetOsVersion();

        string GetVmName();

        string GetHostname();

        string GetUser();
    }
}