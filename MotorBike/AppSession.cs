using System.Collections.Generic;
using MotorBike.Models;

namespace MotorBike;

/// <summary>
/// Holds the current logged-in user's session info.
/// Set after successful login, used by audit fields throughout the app.
/// </summary>
public static class AppSession
{
    /// <summary>The User_ID of the currently logged-in user. Null if not logged in.</summary>
    public static int? CurrentUserId { get; set; }

    /// <summary>The UserName of the currently logged-in user.</summary>
    public static string? CurrentUserName { get; set; }

    /// <summary>Stores permissions for each screen. Key: FrmID, Value: Comma-separated abilities</summary>
    public static Dictionary<int, string> UserPermissions { get; set; } = new();

    /// <summary>
    /// Checks if the current user has the requested ability on the specified screen.
    /// Example: AppSession.HasPermission(ScreenId.Sales, AppAbility.View)
    /// </summary>
    public static bool HasPermission(ScreenId screen, string ability)
    {
        // For admin / first user, you might want to bypass permissions, but for now we strictly check UserPermissions
        if (UserPermissions.TryGetValue((int)screen, out var abilities))
        {
            if (string.IsNullOrWhiteSpace(abilities)) return false;
            var parts = abilities.Split(',');
            foreach (var part in parts)
            {
                if (part.Trim().Equals(ability, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
