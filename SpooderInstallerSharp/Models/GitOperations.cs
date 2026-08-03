using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SpooderInstallerSharp.Models
{
    public class GitOperations
    {
        public GitOperations()
        {
            // No longer need AppendToConsoleOutput - using static ConsoleMessenger
        }

        public async Task<bool> UpdateRepository(string repoPath, string? targetBranch = null)
        {
            var appSettings = SettingsManager.LoadSettings();
            
            try
            {
                using (var repo = new Repository(repoPath))
                {
                    ConsoleMessenger.AddInfoMessage("Updating Spooder repository...");

                    // Ensure user folder is properly ignored
                    await EnsureUserFolderIgnored(repoPath);

                    // Check if there are any uncommitted changes to tracked files
                    var status = repo.RetrieveStatus();
                    var trackedChanges = status.Where(item => item.State != FileStatus.Ignored && 
                                                            item.State != FileStatus.NewInIndex && 
                                                            item.State != FileStatus.NewInWorkdir).ToList();
                    
                    if (trackedChanges.Any())
                    {
                        ConsoleMessenger.AddWarningMessageF("Found {0} uncommitted changes to tracked files. These will be discarded during update.", trackedChanges.Count);
                        foreach (var change in trackedChanges.Take(5)) // Show first 5 changes
                        {
                            ConsoleMessenger.AddDebugMessageF("  {0}: {1}", change.State, change.FilePath);
                        }
                        if (trackedChanges.Count > 5)
                        {
                            ConsoleMessenger.AddDebugMessageF("  ... and {0} more files", trackedChanges.Count - 5);
                        }
                    }

                    // Fetch latest changes from remote
                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                    ConsoleMessenger.AddInfoMessage("Fetching latest changes...");
                    Commands.Fetch(repo, remote.Name, refSpecs, null, "Fetching updates");

                    // Determine target branch
                    string branchToUpdate = targetBranch ?? appSettings.SelectedBranch ?? "main";

                    // Check if we need to switch branches
                    var currentBranch = repo.Head.FriendlyName;
                    if (currentBranch != branchToUpdate)
                    {
                        ConsoleMessenger.AddInfoMessageF("Switching from branch '{0}' to '{1}'", currentBranch, branchToUpdate);

                        // Try to find the branch locally first
                        var localBranch = repo.Branches[branchToUpdate];
                        if (localBranch == null)
                        {
                            // Create local branch tracking remote
                            var remoteBranchToTrack = repo.Branches[$"origin/{branchToUpdate}"];
                            if (remoteBranchToTrack == null)
                            {
                                ConsoleMessenger.AddErrorMessageF("Branch '{0}' not found on remote.", branchToUpdate);
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
                        ConsoleMessenger.AddInfoMessageF("Resetting to latest {0}...", remoteBranchName);
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);
                        ConsoleMessenger.AddSuccessMessage("Repository updated successfully.");
                        ConsoleMessenger.AddInfoMessage("User folder and other ignored files remain intact.");
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessageF("Remote branch {0} not found.", remoteBranchName);
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
                ConsoleMessenger.AddErrorMessageF("Error updating repository: {0}", ex.Message);
                return false;
            }
        }

        public void CloneRepository(string repoUrl, string localPath, string branch = "main")
        {
            ConsoleMessenger.AddInfoMessageF("Cloning Spooder repository on {0}...", branch);
            var cloneOptions = new CloneOptions
            {
                BranchName = branch,
                OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                {
                    ConsoleMessenger.AddDebugMessageF("Checked out {0} of {1} steps.", completedSteps, totalSteps);
                }
            };
            try
            {
                Repository.Clone(repoUrl, localPath, cloneOptions);
                ConsoleMessenger.AddSuccessMessageF("Repository cloned to {0}", localPath);
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error cloning repository: {0}", ex.Message);
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
                    ConsoleMessenger.AddInfoMessage("Adding user folder to .gitignore...");
                    gitignoreContent.Add("");
                    gitignoreContent.Add("# User configuration and data");
                    gitignoreContent.Add("user/");

                    await File.WriteAllLinesAsync(gitignorePath, gitignoreContent);
                    ConsoleMessenger.AddSuccessMessage("User folder added to .gitignore.");
                }
                else
                {
                    ConsoleMessenger.AddDebugMessage("User folder is already in .gitignore.");
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Could not update .gitignore: {0}", ex.Message);
            }
        }
    }
}