using CloudUnify.Core.Authentication;

namespace CloudUnify.Core;

public class CloudUnifyManager {
    private readonly Dictionary<string, GoogleAuthHelper> _authHelpers = new();
    private readonly CloudUnify _cloudUnify = new();
    private readonly Dictionary<string, ICloudProvider> _providers = new();
}