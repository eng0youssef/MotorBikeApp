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
}
