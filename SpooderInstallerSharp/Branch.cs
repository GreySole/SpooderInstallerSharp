using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public class Branch
    {
        public string name { get; set; }

        public static async Task<List<string>> FetchBranchNamesAsync()
        {
            var branchNames = new List<string>();
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request"); // GitHub API requires a User-Agent header
                var response = await httpClient.GetStringAsync("https://api.github.com/repos/greysole/Spooder/branches");
                var branches = JsonSerializer.Deserialize<List<Branch>>(response);

                if (branches != null)
                {
                    foreach (var branch in branches)
                    {
                        branchNames.Add(branch.name);
                    }
                }
            }
            return branchNames;
        }
    }
}