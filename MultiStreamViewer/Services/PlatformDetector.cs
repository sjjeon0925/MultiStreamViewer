using System;

namespace MultiStreamViewer.Services;

public static class PlatformDetector
{
    public static string Detect(string url)
    {
        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out Uri? uri))
        {
            return "Unknown";
        }

        string host = uri.Host.ToLowerInvariant();

        // CHZZK
        if (host == "chzzk.naver.com" ||
            host.EndsWith(".chzzk.naver.com"))
        {
            return "CHZZK";
        }

        // Twitch
        if (host == "twitch.tv" ||
            host == "www.twitch.tv" ||
            host.EndsWith(".twitch.tv"))
        {
            return "Twitch";
        }

        // SOOP
        if (host == "sooplive.co.kr" ||
            host == "www.sooplive.co.kr" ||
            host.EndsWith(".sooplive.co.kr") ||
            host == "sooplive.com" ||
            host == "www.sooplive.com" ||
            host.EndsWith(".sooplive.com"))
        {
            return "SOOP";
        }

        // 씨미
        if (host == "ci.me" ||
            host == "www.ci.me" ||
            host.EndsWith(".ci.me"))
        {
            return "CIME";
        }

        // YouTube
        if (host == "youtube.com" ||
            host == "www.youtube.com" ||
            host == "m.youtube.com" ||
            host == "youtu.be" ||
            host == "www.youtu.be" ||
            host.EndsWith(".youtube.com"))
        {
            return "YouTube";
        }

        return "Unknown";
    }
}