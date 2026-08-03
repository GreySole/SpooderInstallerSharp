using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public class Branch
    {
        public string name { get; set; } = string.Empty;

        public static async Task<List<string>> FetchBranchNamesAsync()
        {
            var branchNames = new List<string>();
            
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request"); // GitHub API requires a User-Agent header
                
                var response = await httpClient.GetStringAsync("https://api.github.com/repos/greysole/Spooder/branches");
                var branches = JsonSerializer.Deserialize<List<Branch>>(response);

                if (branches != null)
                {
                    foreach (var branch in branches)
                    {
                        if (!string.IsNullOrEmpty(branch.name))
                        {
                            branchNames.Add(branch.name);
                            
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Log or handle HTTP request errors
                System.Diagnostics.Debug.WriteLine($"HTTP error fetching branches: {ex.Message}");
            }
            catch (JsonException ex)
            {
                // Log or handle JSON deserialization errors
                System.Diagnostics.Debug.WriteLine($"JSON error deserializing branches: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log or handle other unexpected errors
                System.Diagnostics.Debug.WriteLine($"Unexpected error fetching branches: {ex.Message}");
            }
            
            return branchNames;
        }
    }
}