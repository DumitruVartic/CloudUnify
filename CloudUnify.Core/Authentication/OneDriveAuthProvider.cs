namespace CloudUnify.Core.Authentication;

public class OneDriveAuthProvider : IAuthProvider {
    public Task<string> AuthenticateAsync(string userId) {
        throw new NotImplementedException();
    }

    public Task RevokeTokenAsync(string userId) {
        throw new NotImplementedException();
    }
}