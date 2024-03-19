namespace Bogers.Chapoco.Api;

public class AuthenticationConfiguration
{
    /// <summary>
    /// Password used to authenticate -> gain access to the application
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Disable authentication in its entirety
    /// </summary>
    public bool Disabled { get; set; }
}