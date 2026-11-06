using System;

namespace MultiStreamViewer.Services;

public static class PlatformDetector
{
    // 입력 문자열에 스킴이 없으면 https://를 붙이고, 유효한 HTTP/HTTPS URI인지 검사한다.
    public static bool TryNormalizeHttpUri(
        string? rawInput,
        out Uri? uri,
        out string normalizedUrl)
    {
        uri = null;
        normalizedUrl = string.Empty;

        string trimmed = rawInput?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return false;

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        uri = parsed;
        normalizedUrl = parsed.AbsoluteUri;
        return true;
    }

    public static bool IsChzzkUri(string? url) =>
        Detect(url) == "CHZZK";

    public static string Detect(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return "Unknown";
        }

        string host = uri.Host.ToLowerInvariant();

        // CHZZK
        if (host == "chzzk.naver.com" ||
            host.EndsWith(".chzzk.naver.com", StringComparison.Ordinal))
        {
            return "CHZZK";
        }

        // Twitch
        if (host == "twitch.tv" ||
            host == "www.twitch.tv" ||
            host.EndsWith(".twitch.tv", StringComparison.Ordinal))
        {
            return "Twitch";
        }

        // SOOP
        if (host == "sooplive.co.kr" ||
            host == "www.sooplive.co.kr" ||
            host.EndsWith(".sooplive.co.kr", StringComparison.Ordinal) ||
            host == "sooplive.com" ||
            host == "www.sooplive.com" ||
            host.EndsWith(".sooplive.com", StringComparison.Ordinal))
        {
            return "SOOP";
        }

        // 씨미
        if (host == "ci.me" ||
            host == "www.ci.me" ||
            host.EndsWith(".ci.me", StringComparison.Ordinal))
        {
            return "CIME";
        }

        // YouTube
        if (host == "youtube.com" ||
            host == "www.youtube.com" ||
            host == "m.youtube.com" ||
            host == "youtu.be" ||
            host == "www.youtu.be" ||
            host.EndsWith(".youtube.com", StringComparison.Ordinal))
        {
            return "YouTube";
        }

        return "Unknown";
    }
}
