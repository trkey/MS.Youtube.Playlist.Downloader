﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Google.GData.Client;
using Google.YouTube;
using MS.Youtube.Downloader.Service.Youtube;

namespace MS.Youtube.Downloader.Service
{
    public class DownloaderService
    {
        private readonly YouTubeRequestSettings _settings;

        public DownloaderService()
        {
            _settings = new YouTubeRequestSettings(
                "MS.Youtube.Downloader",
                "AI39si76x-DO4bui7H1o0P6x8iLHPBvQ24exnPiM8McsJhVW_pnCWXOXAa1D8-ymj0Bm07XrtRqxBC7veH6flVIYM7krs36kQg" //key
            ) {AutoPaging = true, PageSize = 50};
        }

        public async Task<ObservableCollection<Playlist>> GetPlaylistsAsync(string userName, int startIndex = 1)
        {
            var list = new ObservableCollection<Playlist>();
            list.Add(new Playlist { Title = "Favorites", Content = userName});

            await Task.Factory.StartNew(() => {
                var request = new YouTubeRequest(_settings);
                var items = request.GetPlaylistsFeed(userName);
                foreach(var item in items.Entries) list.Add(item);
            }).ConfigureAwait(false);
            return list;
        }

        public async Task<ObservableCollection<YoutubeEntry>> GetPlaylistAsync(Playlist playlist)
        {
            var request = new YouTubeRequest(_settings);
            if (playlist.Title == "Favorites") {
                Feed<Video> items = null;
                await Task.Factory.StartNew(() => {
                    items = request.GetFavoriteFeed(playlist.Content);
                }).ConfigureAwait(false); 
                return GetYoutubeEntries(items);
            } else {
                Feed<PlayListMember> items = null;
                await Task.Factory.StartNew(() => {
                    items = request.GetPlaylist(playlist);
                }).ConfigureAwait(false);
                return GetYoutubeEntries(items);
            }
        }

        public async Task<ObservableCollection<YoutubeEntry>> GetPlaylistAsync(Uri uri)
        {
            var id = GetPlaylistId(uri);
            if(String.IsNullOrEmpty(id)) return new ObservableCollection<YoutubeEntry>();
            var request = new YouTubeRequest(_settings);
            Feed<PlayListMember> items = null;
            await Task.Factory.StartNew(() => {
                items = request.Get<PlayListMember>(new Uri("http://gdata.youtube.com/feeds/api/playlists/" + id));
            }).ConfigureAwait(false);
            return GetYoutubeEntries(items);
        }

        private static ObservableCollection<YoutubeEntry> GetYoutubeEntries<T>(Feed<T> items) where T : Video, new()
        {
            var list = new ObservableCollection<YoutubeEntry>();
            if (items == null) return list;
            try {
                foreach (var member in items.Entries.Where(member => member.WatchPage != null)) {
                    var firstOrDefault = member.Thumbnails.FirstOrDefault(t => t.Height == "90" && t.Width == "120");
                    list.Add(new YoutubeEntry {
                        Title = member.Title,
                        Url = member.WatchPage.ToString(),
                        Description = member.Description,
                        ThumbnailUrl = (firstOrDefault != null) ? firstOrDefault.Url : ""
                    });
                }
            }
            catch {
                //
            }
            return list;
        }

        public YoutubeUrl GetUrlType(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return new YoutubeUrl {Type = YoutubeUrlType.Unknown};
            return GetUrlType(uri);
        }

        public YoutubeUrl GetUrlType(Uri u)
        {
            var url = new YoutubeUrl {Uri = u, Type = YoutubeUrlType.Unknown};

            var surl = u.ToString();
            if (surl.StartsWith("https://")) {
                surl = "http://" + surl.Substring(8);
            } else if (!surl.StartsWith("http://")) {
                surl = "http://" + url;
            }

            surl = surl.Replace("youtu.be/", "youtube.com/watch?v=");
            surl = surl.Replace("www.youtube.com", "youtube.com");

            if (surl.StartsWith("http://youtube.com/v/")) {
                surl = surl.Replace("youtube.com/v/", "youtube.com/watch?v=");
            } else if (surl.StartsWith("http://youtube.googleapis.com/v")) {
                surl = surl.Replace("youtube.googleapis.com/v/", "youtube.com/watch?v=");
            } else if (surl.StartsWith("http://youtube.com/watch#")) {
                surl = surl.Replace("youtube.com/watch#", "youtube.com/watch?");
            }
            surl = surl.Replace("//youtube.com", "//www.youtube.com");
            var uri = new Uri(surl);
            url.Uri = uri;
            if (uri.Host != "www.youtube.com") return url;
            var arr = uri.AbsolutePath.Substring(1).Split('/');
            if (arr[0].ToLowerInvariant() == "user") {
                url.Id = arr[1];
                url.Type = YoutubeUrlType.User;
                return url;
            }
            url.Id = GetPlaylistId(uri);
            if (!String.IsNullOrEmpty(url.Id)) {
                url.Type = YoutubeUrlType.Playlist;
                return url;
            }
            try {
                if (arr[0].ToLowerInvariant() == "watch") {
                    url.Id = GetVideoId(uri);
                    url.Type = YoutubeUrlType.Video;
                    url.Uri = uri;
                }
                return url;
            }
            catch {
                return url;
            }
        }

        private string GetVideoId(Uri uri)
        {
            var queryItems = uri.Query.Split('&');
            string id = "";
            if (queryItems.Length > 0 && !String.IsNullOrEmpty(queryItems[0])) {
                foreach (var queryItem in queryItems) {
                    var item = queryItem;
                    if (item[0] == '?') item = item.Substring(1);
                    if (item.Substring(0, 2).ToLowerInvariant() == "v=") {
                        id = item.Substring(2);
                        break;
                    }
                }
            }
            return id;
        }

        private string GetPlaylistId(Uri uri)
        {
            var queryItems = uri.Query.Split('&');
            string id = "";
            if (queryItems.Length > 0 && !String.IsNullOrEmpty(queryItems[0])) {
                foreach (var queryItem in queryItems) {
                    var item = queryItem;
                    if (item[0] == '?') item = item.Substring(1);
                    if (item.Substring(0, 5).ToLowerInvariant() == "list=") {
                        id = item.Substring(5);
                        if (id.Substring(0, 2).ToLowerInvariant() == "pl") id = id.Substring(2);
                        break;
                    }
                }
            }
            return id;
        }
    }
}
