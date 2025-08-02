using LibGit2Sharp;
using SpooderInstallerSharp.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SpooderInstallerSharp.ViewModels
{
    public class GitOperations
    {
        private readonly Action<string> AppendToConsoleOutput;

        public GitOperations(Action<string> appendToConsoleOutput)
        {
            AppendToConsoleOutput = appendToConsoleOutput;
        }

        public async Task<bool> UpdateRepository(string repoPath, string? targetBranch = null)
        {
            var appSettings = SettingsManager.LoadSettings();
            
            try
            {
                using (var repo = new Repository(repoPath))
                {
                    AppendToConsoleOutput("Updating Spooder repository...");

                    // Ensure user folder is properly ignored
                    await EnsureUserFolderIgnored(repoPath);

                    // Check if there are any uncommitted changes to tracked files
                    var status = repo.RetrieveStatus();
                    var trackedChanges = status.Where(item => item.State != FileStatus.Ignored && 
                                                            item.State != FileStatus.NewInIndex && 
                                                            item.State != FileStatus.NewInWorkdir).ToList();
                    
                    if (trackedChanges.Any())
                    {
                        AppendToConsoleOutput($"Warning: Found {trackedChanges.Count} uncommitted changes to tracked files. These will be discarded during update.");
                        foreach (var change in trackedChanges.Take(5)) // Show first 5 changes
                        {
                            AppendToConsoleOutput($"  {change.State}: {change.FilePath}");
                        }
                        if (trackedChanges.Count > 5)
                        {
                            AppendToConsoleOutput($"  ... and {trackedChanges.Count - 5} more files");
                        }
                    }

                    // Fetch latest changes from remote
                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                    AppendToConsoleOutput("Fetching latest changes...");
                    Commands.Fetch(repo, remote.Name, refSpecs, null, "Fetching updates");

                    // Determine target branch
                    string branchToUpdate = targetBranch ?? appSettings.SelectedBranch ?? "main";

                    // Check if we need to switch branches
                    var currentBranch = repo.Head.FriendlyName;
                    if (currentBranch != branchToUpdate)
                    {
                        AppendToConsoleOutput($"Switching from branch '{currentBranch}' to '{branchToUpdate}'");

                        // Try to find the branch locally first
                        var localBranch = repo.Branches[branchToUpdate];
                        if (localBranch == null)
                        {
                            // Create local branch tracking remote
                            var remoteBranchToTrack = repo.Branches[$"origin/{branchToUpdate}"];
                            if (remoteBranchToTrack == null)
                            {
                                AppendToConsoleOutput($"Branch '{branchToUpdate}' not found on remote.");
                                return false;
                            }

                            localBranch = repo.CreateBranch(branchToUpdate, remoteBranchToTrack.Tip);
                            repo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranchToTrack.CanonicalName);
                        }

                        // Checkout the target branch
                        var checkoutOptions = new CheckoutOptions()
                        {
                            CheckoutModifiers = CheckoutModifiers.Force // Force checkout to avoid conflicts
                        };
                        Commands.Checkout(repo, localBranch, checkoutOptions);
                    }

                    // Reset to latest remote commit (hard reset)
                    var remoteBranchName = $"origin/{branchToUpdate}";
                    var remoteBranch = repo.Branches[remoteBranchName];
                    if (remoteBranch != null)
                    {
                        AppendToConsoleOutput($"Resetting to latest {remoteBranchName}...");
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);
                        AppendToConsoleOutput("Repository updated successfully.");
                        AppendToConsoleOutput("User folder and other ignored files remain intact.");
                    }
                    else
                    {
                        AppendToConsoleOutput($"Remote branch {remoteBranchName} not found.");
                        return false;
                    }

                    // Update the selected branch in settings if we switched
                    if (targetBranch != null && targetBranch != appSettings.SelectedBranch)
                    {
                        appSettings.SelectedBranch = targetBranch;
                        SettingsManager.SaveSettings(appSettings);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error updating repository: {ex.Message}");
                return false;
            }
        }

        public void CloneRepository(string repoUrl, string localPath, string branch = "main")
        {
            AppendToConsoleOutput($"Cloning Spooder repository on {branch}...");
            var cloneOptions = new CloneOptions
            {
                BranchName = branch,
                OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                {
                    AppendToConsoleOutput($"Checked out {completedSteps} of {totalSteps} steps.");
                }
            };
            try
            {
                Repository.Clone(repoUrl, localPath, cloneOptions);
                AppendToConsoleOutput($"Repository cloned to {localPath}");
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error cloning repository: {ex.Message}");
            }
        }

        private async Task EnsureUserFolderIgnored(string repoPath)
        {
            var gitignorePath = Path.Combine(repoPath, ".gitignore");

            try
            {
                // Check if .gitignore exists and contains user folder entry
                var gitignoreContent = new List<string>();

                if (File.Exists(gitignorePath))
                {
                    gitignoreContent.AddRange(await File.ReadAllLinesAsync(gitignorePath));
                }

                // Check if user folder is already ignored
                bool userFolderIgnored = gitignoreContent.Any(line =>
                    line.Trim().Equals("user/", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("user", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("/user/", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("/user", StringComparison.OrdinalIgnoreCase));

                if (!userFolderIgnored)
                {
                    AppendToConsoleOutput("Adding user folder to .gitignore...");
                    gitignoreContent.Add("");
                    gitignoreContent.Add("# User configuration and data");
                    gitignoreContent.Add("user/");

                    await File.WriteAllLinesAsync(gitignorePath, gitignoreContent);
                    AppendToConsoleOutput("User folder added to .gitignore.");
                }
                else
                {
                    AppendToConsoleOutput("User folder is already in .gitignore.");
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Could not update .gitignore: {ex.Message}");
            }
        }
    }
}