namespace EasyOps.Services;

public interface IIdentityManagementService
{
    Task<string> GetRampId(string gameId);
}