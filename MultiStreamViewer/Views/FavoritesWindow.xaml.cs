using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

using MultiStreamViewer.Models;
using MultiStreamViewer.Services;

namespace MultiStreamViewer.Views;

public partial class FavoritesWindow : Window
{
    private readonly ObservableCollection<Favorite> favorites;

    // 사용자가 고른 즐겨찾기 (ShowDialog가 true일 때만 값이 있다)
    public Favorite? Chosen { get; private set; }

    public FavoritesWindow(string initialUrl)
    {
        InitializeComponent();

        favorites = new ObservableCollection<Favorite>(FavoritesStore.Load());

        FavoriteList.ItemsSource = favorites;

        // 상단 주소창에 입력해 둔 주소를 바로 추가할 수 있게 채워둔다.
        UrlBox.Text = initialUrl;
    }

    private void FavoriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavoriteList.SelectedItem is Favorite favorite)
        {
            Chosen = favorite;

            DialogResult = true;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Favorite favorite })
        {
            favorites.Remove(favorite);

            FavoritesStore.Save(favorites);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlBox.Text.Trim();

        if (url.Length == 0)
        {
            MessageText.Text = "방송 주소를 입력하세요.";

            return;
        }

        // 스킴 없이 입력해도 https로 보정한다.
        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageText.Text = "올바른 주소가 아닙니다.";

            return;
        }

        string name = NameBox.Text.Trim();

        if (name.Length == 0)
            name = uri.Host;

        favorites.Add(new Favorite(name, url));

        FavoritesStore.Save(favorites);

        NameBox.Clear();
        UrlBox.Clear();

        MessageText.Text = string.Empty;
    }
}
