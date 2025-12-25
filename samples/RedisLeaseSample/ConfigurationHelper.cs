using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RedisLeaseSample;

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
            
        // Check for bracket notation: [YOUR_REDIS_CACHE]
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
            
            Console.WriteLine("Configuration file not found. Let's set up your Azure Redis connection.");
            Console.WriteLine();
            
            // Step 1: Prompt for Redis cache name
            var redisCacheName = PromptForRedisCacheName();
            if (string.IsNullOrEmpty(redisCacheName))
            {
                WriteColoredText("Setup cancelled.", ConsoleColor.Yellow);
                return false;
            }
            
            // Step 2: Prompt for key prefix
            var keyPrefix = PromptForKeyPrefix();
            
            // Step 3: Prompt for database number
            var database = PromptForDatabase();
            
            // Step 4: Prompt for authentication mode
            var authMode = PromptForAuthenticationMode();
            
            string? connectionString = null;
            
            if (authMode == 1)
            {
                // Connection String mode
                connectionString = PromptForConnectionString(redisCacheName);
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
                RedisCacheName = redisCacheName,
                KeyPrefix = keyPrefix,
                Database = database,
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
            RedisLeasing = new
            {
                Endpoint = input.AuthMode == 2 
                    ? $"{input.RedisCacheName}.redis.cache.windows.net:6380"
                    : (string?)null,
                ConnectionString = input.AuthMode == 1 
                    ? input.ConnectionString
                    : (string?)null,
                KeyPrefix = input.KeyPrefix,
                Database = input.Database
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
        WriteColoredText("  AZURE REDIS LEASE SAMPLE - FIRST-TIME SETUP", ConsoleColor.Cyan);
        Console.WriteLine("═".PadRight(63, '═'));
        Console.WriteLine();
    }
    
    private static string PromptForRedisCacheName()
    {
        Console.WriteLine("1. What is your Azure Redis Cache name?");
        Console.WriteLine("   (Format: lowercase letters, numbers, and hyphens)");
        WriteColoredText("   Example: myrediscache", ConsoleColor.DarkGray);
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("   Cache Name: ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            
            if (ValidateRedisCacheName(input))
            {
                return input;
            }
            
            WriteColoredText("   Invalid format. Use only lowercase letters, numbers, and hyphens.", ConsoleColor.Red);
            Console.WriteLine();
        }
    }
    
    private static string PromptForKeyPrefix()
    {
        Console.WriteLine();
        Console.WriteLine("2. What key prefix should be used for leases?");
        WriteColoredText("   Default: lease:", ConsoleColor.DarkGray);
        Console.WriteLine();
        Console.Write("   Key Prefix [lease:]: ");
        
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? "lease:" : input;
    }
    
    private static int PromptForDatabase()
    {
        Console.WriteLine();
        Console.WriteLine("3. Which Redis database should be used (0-15)?");
        WriteColoredText("   Default: 0", ConsoleColor.DarkGray);
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("   Database [0]: ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }
            
            if (int.TryParse(input, out var database) && database >= 0 && database <= 15)
            {
                return database;
            }
            
            WriteColoredText("   Please enter a number between 0 and 15.", ConsoleColor.Red);
        }
    }
    
    private static int PromptForAuthenticationMode()
    {
        Console.WriteLine();
        Console.WriteLine("4. How would you like to authenticate?");
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
    
    private static string? PromptForConnectionString(string cacheName)
    {
        Console.WriteLine();
        Console.WriteLine("5. Enter your Azure Redis connection string:");
        WriteColoredText("   (Find in Azure Portal > Redis Cache > Access Keys)", ConsoleColor.DarkGray);
        WriteColoredText($"   Format: {cacheName}.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True", ConsoleColor.DarkGray);
        Console.WriteLine();
        Console.Write("   Connection String: ");
        
        return Console.ReadLine()?.Trim();
    }
    
    private static bool ValidateRedisCacheName(string name)
    {
        // Redis cache names must contain only lowercase letters, numbers, and hyphens
        // and must be between 1 and 63 characters
        if (name.Length < 1 || name.Length > 63)
            return false;
            
        return Regex.IsMatch(name, "^[a-z0-9-]+$");
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
    public required string RedisCacheName { get; init; }
    public required string KeyPrefix { get; init; }
    public required int Database { get; init; }
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
