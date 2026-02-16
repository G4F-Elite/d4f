using System.Security.Cryptography;
using System.Text;

namespace Engine.AssetPipeline;

internal static class PakEntryKeyBuilder
{
    public static string Compute(
        string path,
        string kind,
        string compiledPath,
        long sizeBytes)
    {
        string payload =
            $"{Normalize(path)}|{Normalize(kind)}|{Normalize(compiledPath)}|{sizeBytes}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace('\\', '/').Trim();
    }
}
