using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace EasyOps.Services;

public class IdentityManagementService(HttpClient httpClient) : IIdentityManagementService
{
    public async Task<string> GetRampId(string gameId)
    {
        var response = await httpClient.GetAsync($"identity?uid={gameId}");
        response.EnsureSuccessStatusCode();
        var idmResponse = await response.Content.ReadAsStringAsync();
        var rampId = JToken.Parse(idmResponse)["identifier"]["externalIds"][0]["id"].ToString();

        return rampId;
    }
}