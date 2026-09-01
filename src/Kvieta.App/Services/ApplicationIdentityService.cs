using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using Kvieta.Core.Models;

namespace Kvieta.App.Services;

public static class ApplicationIdentityService
{
    private static readonly ConcurrentDictionary<string, string> OriginalFileNameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> PublisherCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> HashCache = new(StringComparer.OrdinalIgnoreCase);
    public static AppRule CaptureRule(string executablePath)
    {
        string fullPath = Path.GetFullPath(executablePath);
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(fullPath);
        string publisherName = string.Empty;
        string publisherThumbprint = string.Empty;
        try
        {
            if (!AuthenticodeTrustVerifier.IsTrusted(fullPath)) throw new InvalidDataException("Untrusted signature.");
#pragma warning disable SYSLIB0057
            using X509Certificate2 signer = new(X509Certificate.CreateFromSignedFile(fullPath));
#pragma warning restore SYSLIB0057
            publisherName = signer.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            publisherThumbprint = signer.Thumbprint ?? string.Empty;
        }
        catch
        {
            // Unsigned portable applications are pinned by SHA-256 instead.
        }

        return new AppRule
        {
            Name = string.IsNullOrWhiteSpace(version.ProductName)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : version.ProductName,
            ExecutablePath = fullPath,
            OriginalFileName = version.OriginalFilename ?? Path.GetFileName(fullPath),
            ProductName = version.ProductName ?? string.Empty,
            PublisherName = publisherName,
            PublisherThumbprint = publisherThumbprint,
            Sha256 = ComputeSha256(fullPath),
            RequireSha256 = string.IsNullOrWhiteSpace(publisherThumbprint),
            IncludeChildProcesses = true,
            Mode = AppRuleMode.Blocked,
            DailyLimitMinutes = 60
        };
    }

    public static string ComputeSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static bool MatchesRule(AppRule rule, string executablePath, string? packageFamilyName = null)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(executablePath); }
        catch { return false; }

        bool pathMatch = string.Equals(fullPath, NormalizePath(rule.ExecutablePath), StringComparison.OrdinalIgnoreCase) ||
            rule.LauncherExecutablePaths.Any(path =>
                string.Equals(fullPath, NormalizePath(path), StringComparison.OrdinalIgnoreCase));
        bool packageMatch = !string.IsNullOrWhiteSpace(rule.PackageFamilyName) &&
            string.Equals(rule.PackageFamilyName, packageFamilyName, StringComparison.OrdinalIgnoreCase);
        bool canRelocateBySignature = !string.IsNullOrWhiteSpace(rule.PublisherThumbprint) &&
            !string.IsNullOrWhiteSpace(rule.OriginalFileName);
        if (!pathMatch && !packageMatch && !canRelocateBySignature) return false;

        try
        {
            string cacheKey = GetCacheKey(fullPath);
            if (!string.IsNullOrWhiteSpace(rule.OriginalFileName) &&
                !string.Equals(
                    rule.OriginalFileName,
                    OriginalFileNameCache.GetOrAdd(cacheKey, _ =>
                        FileVersionInfo.GetVersionInfo(fullPath).OriginalFilename ?? Path.GetFileName(fullPath)),
                    StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrWhiteSpace(rule.PublisherThumbprint) &&
                !string.Equals(
                    rule.PublisherThumbprint,
                    PublisherCache.GetOrAdd(cacheKey, _ => ReadTrustedPublisherThumbprint(fullPath)),
                    StringComparison.OrdinalIgnoreCase)) return false;

            if (rule.RequireSha256 &&
                !string.Equals(
                    rule.Sha256,
                    HashCache.GetOrAdd(cacheKey, _ => ComputeSha256(fullPath)),
                    StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        catch { return false; }
    }

    private static string GetCacheKey(string path)
    {
        FileInfo file = new(path);
        return $"{path}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
    }

    private static string ReadTrustedPublisherThumbprint(string path)
    {
        try
        {
            if (!AuthenticodeTrustVerifier.IsTrusted(path)) return string.Empty;
#pragma warning disable SYSLIB0057
            using X509Certificate2 signer = new(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return signer.Thumbprint ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path.Trim(); }
    }
}
