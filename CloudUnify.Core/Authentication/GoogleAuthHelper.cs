namespace CloudUnify.Core.Authentication;

public class GoogleAuthHelper {
    private readonly string _applicationName;
    private readonly string _clientSecretsPath;
    private readonly string _dataStorePath;

    public GoogleAuthHelper(string clientSecretsPath, string applicationName, string dataStorePath) {
        _clientSecretsPath = clientSecretsPath;
        _applicationName = applicationName;
        _dataStorePath = dataStorePath;
    }
}