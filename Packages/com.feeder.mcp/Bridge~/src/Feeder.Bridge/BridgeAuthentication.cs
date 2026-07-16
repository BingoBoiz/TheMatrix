using System.Security.Cryptography;
using System.Text;

namespace Feeder.Bridge;

public static class BridgeAuthentication
{
    public static bool HasValidBearerToken(string? authorization, string? expectedToken)
    {
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(authorization) ||
            !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return FixedTimeEquals(authorization[prefix.Length..], expectedToken);
    }

    public static bool FixedTimeEquals(string? actual, string? expected)
    {
        if (actual is null || expected is null)
            return false;

        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
