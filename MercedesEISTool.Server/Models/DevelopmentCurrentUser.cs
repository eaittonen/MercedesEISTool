namespace MercedesEISTool.Server.Models;

public class DevelopmentCurrentUser : ICurrentUser
{
    public string UserId => "development";
    public string DisplayName => "Development";
}
