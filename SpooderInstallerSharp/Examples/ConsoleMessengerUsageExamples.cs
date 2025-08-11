/*
 * Example Usage of Static ConsoleMessenger
 * 
 * This file demonstrates how to use the static ConsoleMessenger from anywhere in your application.
 * The ConsoleMessenger provides semantic console output with CSS styling capabilities.
 */

using SpooderInstallerSharp.Models;

namespace SpooderInstallerSharp.Examples
{
    public class ConsoleMessengerUsageExamples
    {
        public void BasicUsageExamples()
        {
            // Basic semantic messages - these can be called from ANYWHERE in your app
            ConsoleMessenger.AddErrorMessage("Something went wrong!");
            ConsoleMessenger.AddWarningMessage("This is a warning message");
            ConsoleMessenger.AddSuccessMessage("Operation completed successfully");
            ConsoleMessenger.AddInfoMessage("Here's some information");
            ConsoleMessenger.AddDebugMessage("Debug information for developers");
        }

        public void AdvancedUsageExamples()
        {
            // Custom styled messages
            ConsoleMessenger.AddStyledMessage("Custom red text on yellow background", "text-red", "bg-yellow");
            ConsoleMessenger.AddStyledMessage("Bold blue underlined text", "text-blue", "text-bold", "text-underline");

            // Plain messages without styling
            ConsoleMessenger.AddPlainMessage("This is a plain message without any CSS classes");

            // Conditional messages (only show if condition is true)
            bool debugMode = true;
            bool productionMode = false;

            ConsoleMessenger.AddDebugMessageIf(debugMode, "This debug message will appear");
            ConsoleMessenger.AddErrorMessageIf(productionMode, "This error message will NOT appear");

            // Formatted messages with parameters
            string userName = "John";
            int fileCount = 42;
            ConsoleMessenger.AddInfoMessageF("User {0} has {1} files in their directory", userName, fileCount);
            ConsoleMessenger.AddSuccessMessageF("Successfully processed {0} items", fileCount);
        }

        public void ErrorHandlingExamples()
        {
            try
            {
                // Simulate some operation
                throw new System.InvalidOperationException("Something bad happened");
            }
            catch (System.Exception ex)
            {
                // Log the error with context
                ConsoleMessenger.AddErrorMessageF("Operation failed: {0}", ex.Message);
                ConsoleMessenger.AddDebugMessageF("Stack trace: {0}", ex.StackTrace);
            }

            // Progress reporting
            for (int i = 1; i <= 5; i++)
            {
                ConsoleMessenger.AddInfoMessageF("Processing step {0} of 5...", i);
                // Simulate work
                System.Threading.Thread.Sleep(100);
            }
            ConsoleMessenger.AddSuccessMessage("All steps completed!");
        }

        public void BusinessLogicExamples()
        {
            // File operations
            string filePath = @"C:\temp\myfile.txt";
            if (System.IO.File.Exists(filePath))
            {
                ConsoleMessenger.AddSuccessMessageF("File found: {0}", filePath);
            }
            else
            {
                ConsoleMessenger.AddWarningMessageF("File not found: {0}", filePath);
            }

            // Network operations
            bool internetConnected = CheckInternetConnection();
            ConsoleMessenger.AddInfoMessageIf(internetConnected, "Internet connection is available");
            ConsoleMessenger.AddWarningMessageIf(!internetConnected, "No internet connection detected");

            // Installation progress
            ReportInstallationProgress();
        }

        private bool CheckInternetConnection()
        {
            // Simulate internet check
            return true;
        }

        private void ReportInstallationProgress()
        {
            ConsoleMessenger.AddInfoMessage("Starting installation...");
            ConsoleMessenger.AddInfoMessage("Downloading dependencies...");
            ConsoleMessenger.AddWarningMessage("Some optional components were skipped");
            ConsoleMessenger.AddInfoMessage("Configuring application...");
            ConsoleMessenger.AddSuccessMessage("Installation completed successfully!");
        }
    }

    // Example: Using in a service class
    public class FileService
    {
        public bool ProcessFile(string filePath)
        {
            ConsoleMessenger.AddInfoMessageF("Processing file: {0}", filePath);

            if (!System.IO.File.Exists(filePath))
            {
                ConsoleMessenger.AddErrorMessageF("File not found: {0}", filePath);
                return false;
            }

            try
            {
                // Simulate file processing
                var content = System.IO.File.ReadAllText(filePath);
                ConsoleMessenger.AddSuccessMessageF("Successfully read {0} characters from file", content.Length);
                return true;
            }
            catch (System.Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Failed to process file: {0}", ex.Message);
                return false;
            }
        }
    }

    // Example: Using in a business logic class
    public class InstallationService
    {
        public void InstallSoftware(string softwareName)
        {
            ConsoleMessenger.AddInfoMessageF("Starting installation of {0}...", softwareName);

            // Check prerequisites
            if (!CheckPrerequisites())
            {
                ConsoleMessenger.AddErrorMessage("Prerequisites check failed. Installation aborted.");
                return;
            }

            // Download
            ConsoleMessenger.AddInfoMessage("Downloading installation files...");
            if (!DownloadFiles())
            {
                ConsoleMessenger.AddErrorMessage("Download failed. Installation aborted.");
                return;
            }

            // Install
            ConsoleMessenger.AddInfoMessage("Installing software...");
            if (!InstallFiles())
            {
                ConsoleMessenger.AddErrorMessage("Installation failed.");
                return;
            }

            ConsoleMessenger.AddSuccessMessageF("{0} has been installed successfully!", softwareName);
        }

        private bool CheckPrerequisites()
        {
            ConsoleMessenger.AddDebugMessage("Checking system requirements...");
            return true; // Simulate success
        }

        private bool DownloadFiles()
        {
            ConsoleMessenger.AddDebugMessage("Downloading from remote server...");
            return true; // Simulate success
        }

        private bool InstallFiles()
        {
            ConsoleMessenger.AddDebugMessage("Extracting and copying files...");
            return true; // Simulate success
        }
    }
}