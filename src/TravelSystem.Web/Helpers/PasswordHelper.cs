using Microsoft.AspNetCore.Identity;
using TravelSystem.Shared.Models;

namespace TravelSystem.Web.Helpers;

public static class PasswordHelper
{
    public static string HashPassword(User user, string password)
    {
        var hasher = new PasswordHasher<User>();
        return hasher.HashPassword(user, password);
    }

    public static bool VerifyPassword(User user, string password, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var hasher = new PasswordHasher<User>();
        PasswordVerificationResult verifyResult;
        try
        {
            verifyResult = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (FormatException)
        {
            verifyResult = PasswordVerificationResult.Failed;
        }

        if (verifyResult == PasswordVerificationResult.Success)
        {
            return true;
        }

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            needsUpgrade = true;
            return true;
        }

        // Backward compatibility for legacy plaintext passwords.
        if (string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
        {
            needsUpgrade = true;
            return true;
        }

        return false;
    }
}
