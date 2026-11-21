using PosApp.Models;

namespace PosApp.Services;

public static class Session
{
    public static UserAccount? CurrentUser { get; set; }
}
