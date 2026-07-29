using System.Text;
using SteamDepotDownload.Steam.Shared.Auth;

namespace SteamDepotDownload.Steam.Core.Auth;

internal sealed class CConsoleAuthenticator : ISteamAuthenticator
{
    private readonly bool _preferTwoFactorCode;

    public CConsoleAuthenticator(bool preferTwoFactorCode = false)
    {
        _preferTwoFactorCode = preferTwoFactorCode;
    }

    public Task<string> GetTwoFactorCodeAsync(bool previousAttemptIncorrect, CancellationToken ct)
    {
        if (previousAttemptIncorrect)
        {
            Console.Error.WriteLine("That Steam Guard code was not accepted.");
        }

        return Task.FromResult(Prompt("Steam Guard code from your mobile authenticator: "));
    }

    public Task<string> GetEmailCodeAsync(string? email, bool previousAttemptIncorrect, CancellationToken ct)
    {
        if (previousAttemptIncorrect)
        {
            Console.Error.WriteLine("That code was not accepted.");
        }

        var where = string.IsNullOrEmpty(email) ? "your email" : email;
        return Task.FromResult(Prompt($"Steam Guard code sent to {where}: "));
    }

    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken ct)
    {
        if (!_preferTwoFactorCode)
        {
            Console.Error.WriteLine("Approve this login in the Steam mobile app.");
        }

        return Task.FromResult(!_preferTwoFactorCode);
    }

    public Task<string> GetPasswordAsync(string username, CancellationToken ct)
        => Task.FromResult(ReadPassword($"Password for {username}: "));

    public Task OnQrChallengeUrlAsync(string challengeUrl, CancellationToken ct)
    {
        Console.Error.WriteLine();
        foreach (var line in CQrRenderer.Render(challengeUrl))
        {
            Console.Error.WriteLine(line);
        }

        Console.Error.WriteLine("Scan the code with the Steam mobile app to log in.");
        Console.Error.WriteLine(challengeUrl);
        return Task.CompletedTask;
    }

    private static string Prompt(string message)
    {
        Console.Error.Write(message);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static string ReadPassword(string message)
    {
        Console.Error.Write(message);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var password = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Error.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Error.Write('*');
            }
        }
    }
}
