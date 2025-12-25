using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlobLeaseSample;

/// <summary>
/// Helper class for interactive configuration setup and validation.
/// </summary>
public static class ConfigurationHelper
{
    private const string LocalConfigFileName = "appsettings.Local.json";
    
    /// <summary>
    /// Checks if the local configuration file exists.
    /// </summary>
    public static bool CheckLocalConfigurationExists()
    {
        return File.Exists(LocalConfigFileName);
    }
    
    /// <summary>
    /// Checks if a value contains placeholder patterns.
    /// </summary>
    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
            
        // Check for bracket notation: [YOUR_STORAGE_ACCOUNT]
        if (value.Contains('[') && value.Contains(']'))
            return true;
            
        // Check for common placeholder keywords
        var placeholderKeywords = new[] { "YOUR", "REPLACE", "EXAMPLE", "PLACEHOLDER" };
        return placeholderKeywords.Any(keyword => 
            value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Runs the interactive configuration setup wizard.
    /// </summary>
    public static async Task<bool> RunInteractiveSetup()
    {
        try
        {
            DisplayWelcomeBanner();
            
            Console.WriteLine("Configuration file not found. Let's set up your Azure connection.");
            Console.WriteLine();
            
            // Step 1: Prompt for storage account name
            var storageAccount = PromptForStorageAccount();
            if (string.IsNullOrEmpty(storageAccount))
            {
                WriteColoredText("Setup cancelled.", ConsoleColor.Yellow);
                return false;
            }
            
            // Step 2: Prompt for container name
            var containerName = PromptForContainerName();
            
            // Step 3: Prompt for authentication mode
            var authMode = PromptForAuthenticationMode();
            
            string? connectionString = null;
            
            if (authMode == 1)
            {
                // Connection String mode
                connectionString = PromptForConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    WriteColoredText("Setup cancelled.", ConsoleColor.Yellow);
                    return false;
                }
            }
            else
            {
                // DefaultAzureCredential mode - validate Azure CLI
                var validationResult = await ValidateAzureCliAuthentication();
                if (!validationResult.IsSuccess)
                {
                    WriteColoredText($"✗ {validationResult.Message}", ConsoleColor.Red);
                    WriteColoredText("\nPlease run 'az login' first, then try again.", ConsoleColor.Yellow);
                    return false;
                }
                
                WriteColoredText($"✓ {validationResult.Message}", ConsoleColor.Green);
            }
            
            // Generate and save configuration
            var configInput = new ConfigurationInput
            {
                StorageAccountName = storageAccount,
                ContainerName = containerName,
                AuthMode = authMode,
                ConnectionString = connectionString
            };
            
            GenerateLocalConfiguration(configInput);
            
            Console.WriteLine();
            Console.WriteLine("━".PadRight(63, '━'));
            Console.WriteLine();
            WriteColoredText($"✓ Configuration saved to: {LocalConfigFileName}", ConsoleColor.Green);
            WriteColoredText("✓ Starting application...", ConsoleColor.Green);
            Console.WriteLine();
            Console.WriteLine("═".PadRight(63, '═'));
            Console.WriteLine();
            
            return true;
        }
        catch (Exception ex)
        {
            WriteColoredText($"Error during setup: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }
    
    /// <summary>
    /// Validates Azure CLI authentication status.
    /// </summary>
    public static async Task<ValidationResult> ValidateAzureCliAuthentication()
    {
        try
        {
            Console.WriteLine();
            WriteColoredText("✓ Checking Azure CLI authentication...", ConsoleColor.Cyan);
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "az",
                Arguments = "account show --output json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new ValidationResult
                {
                    IsSuccess = false,
                    Message = "Azure CLI not found. Please install Azure CLI."
                };
            }
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                return new ValidationResult
                {
                    IsSuccess = false,
                    Message = "Not authenticated with Azure CLI."
                };
            }
            
            // Parse JSON to extract user and subscription info
            try
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                var userName = root.GetProperty("user").GetProperty("name").GetString();
                var subscriptionName = root.GetProperty("name").GetString();
                
                WriteColoredText($"✓ Authenticated as: {userName}", ConsoleColor.Green);
                WriteColoredText($"✓ Subscription: {subscriptionName}", ConsoleColor.Green);
                
                return new ValidationResult
                {
                    IsSuccess = true,
                    Message = $"Authenticated as {userName}"
                };
            }
            catch
            {
                return new ValidationResult
                {
                    IsSuccess = true,
                    Message = "Azure CLI authenticated"
                };
            }
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsSuccess = false,
                Message = $"Error validating Azure CLI: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Generates the local configuration file.
    /// </summary>
    public static void GenerateLocalConfiguration(ConfigurationInput input)
    {
        var config = new
        {
            BlobLeasing = new
            {
                StorageAccountUri = input.AuthMode == 2 
                    ? $"https://{input.StorageAccountName}.blob.core.windows.net"
                    : (string?)null,
                ConnectionString = input.AuthMode == 1 
                    ? input.ConnectionString
                    : (string?)null,
                ContainerName = input.ContainerName
            }
        };
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(LocalConfigFileName, json);
    }
    
    private static void DisplayWelcomeBanner()
    {
        Console.WriteLine();
        Console.WriteLine("═".PadRight(63, '═'));
        WriteColoredText("  AZURE BLOB LEASE SAMPLE - FIRST-TIME SETUP", ConsoleColor.Cyan);
        Console.WriteLine("═".PadRight(63, '═'));
        Console.WriteLine();
    }
    
    private static string PromptForStorageAccount()
    {
        Console.WriteLine("1. What is your Azure Storage Account name?");
        Console.WriteLine("   (Format: lowercase letters and numbers only)");
        WriteColoredText("   Example: mystorageaccount123", ConsoleColor.DarkGray);
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("   Account Name: ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            
            if (ValidateStorageAccountName(input))
            {
                return input;
            }
            
            WriteColoredText("   Invalid format. Use only lowercase letters and numbers (3-24 characters).", ConsoleColor.Red);
            Console.WriteLine();
        }
    }
    
    private static string PromptForContainerName()
    {
        Console.WriteLine();
        Console.WriteLine("2. What container name should be used for leases?");
        WriteColoredText("   Default: leases", ConsoleColor.DarkGray);
        Console.WriteLine();
        Console.Write("   Container Name [leases]: ");
        
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? "leases" : input;
    }
    
    private static int PromptForAuthenticationMode()
    {
        Console.WriteLine();
        Console.WriteLine("3. How would you like to authenticate?");
        Console.WriteLine("   1) Connection String (simple - for local development)");
        Console.WriteLine("   2) DefaultAzureCredential (recommended - uses Azure CLI login)");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("   Choice (1 or 2): ");
            var input = Console.ReadLine()?.Trim();
            
            if (input == "1" || input == "2")
            {
                return int.Parse(input);
            }
            
            WriteColoredText("   Please enter 1 or 2.", ConsoleColor.Red);
        }
    }
    
    private static string? PromptForConnectionString()
    {
        Console.WriteLine();
        Console.WriteLine("4. Enter your Azure Storage connection string:");
        WriteColoredText("   (Find in Azure Portal > Storage Account > Access Keys)", ConsoleColor.DarkGray);
        Console.WriteLine();
        Console.Write("   Connection String: ");
        
        return Console.ReadLine()?.Trim();
    }
    
    private static bool ValidateStorageAccountName(string name)
    {
        // Storage account names must be between 3 and 24 characters
        // and may contain only lowercase letters and numbers
        if (name.Length < 3 || name.Length > 24)
            return false;
            
        return Regex.IsMatch(name, "^[a-z0-9]+$");
    }
    
    private static void WriteColoredText(string text, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = originalColor;
    }
}

/// <summary>
/// Input model for configuration generation.
/// </summary>
public class ConfigurationInput
{
    public required string StorageAccountName { get; init; }
    public required string ContainerName { get; init; }
    public required int AuthMode { get; init; }
    public string? ConnectionString { get; init; }
}

/// <summary>
/// Result of Azure CLI validation.
/// </summary>
public class ValidationResult
{
    public required bool IsSuccess { get; init; }
    public required string Message { get; init; }
}
