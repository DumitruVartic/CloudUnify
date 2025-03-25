namespace CloudUnify.Core;

public interface IAuthProvider {
    Task<string> AuthenticateAsync(string userId);
    Task RevokeTokenAsync(string userId);
}