using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using MultiStreamViewer.Models;

namespace MultiStreamViewer.Services;

// 즐겨찾기를 %AppData%\MultiStreamViewer\favorites.json에 저장한다.
public static class FavoritesStore
{
    private static readonly string FilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MultiStreamViewer",
            "favorites.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static List<Favorite> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<Favorite>();

            return JsonSerializer.Deserialize<List<Favorite>>(
                       File.ReadAllText(FilePath),
                       Options)
                   ?? new List<Favorite>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"즐겨찾기 불러오기 실패: {ex}");

            return new List<Favorite>();
        }
    }

    public static void Save(IEnumerable<Favorite> favorites)
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(FilePath)!);

            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(favorites, Options));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"즐겨찾기 저장 실패: {ex}");
        }
    }
}
