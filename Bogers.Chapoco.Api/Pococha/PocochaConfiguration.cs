namespace Bogers.Chapoco.Api.Pococha;

public class PocochaConfiguration
{
    private string _flowsDirectory;

    /// <summary>
    /// Directory where mitm flows are kept, flows contain the information required to access the pococha api
    /// </summary>
    public string FlowsDirectory
    {
        get => _flowsDirectory;
        set => _flowsDirectory = PathHelper.Normalize(value);
    }
}