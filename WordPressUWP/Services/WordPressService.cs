﻿using GalaSoft.MvvmLight;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;
using WordPressUWP.Helpers;
using WordPressUWP.Interfaces;

namespace WordPressUWP.Services
{
    public class WordPressService : ViewModelBase, IWordPressService 
    {
        private WordPressClient _client;
        private ApplicationDataContainer _localSettings;
        private IEnumerable<Post> _first20Posts;


        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get { return _isLoggedIn; }
            set { Set(ref _isLoggedIn, value); }
        }

        private User _currentUser;
        public User CurrentUser
        {
            get { return _currentUser; }
            set { Set(ref _currentUser, value); }
        }

        public WordPressService()
        {
            _client = new WordPressClient(ApiCredentials.WordPressUri);
            _localSettings = ApplicationData.Current.LocalSettings;
            Init();
        }

        public async void Init()
        {

            IsLoggedIn = false;
            var username = _localSettings.ReadString("Username");
            if(username != null)
            {
                // get password
                var jwt = SettingsStorageExtensions.GetCredentialFromLocker(username);
                if(jwt != null && !string.IsNullOrEmpty(jwt.Password))
                {
                    // set jwt
                    _client.SetJWToken(jwt.Password);
                    IsLoggedIn = await _client.IsValidJWToken();
                    if (IsLoggedIn)
                    {
                        CurrentUser = await _client.Users.GetCurrentUser();
                    }
                }
            }
        }

        public async Task<bool> AuthenticateUser(string username, string password)
        {
            _client.AuthMethod = AuthMethod.JWT;
            await _client.RequestJWToken(username, password);
            var isAuthenticated = await IsUserAuthenticated();

            if (isAuthenticated)
            {
                // Store username & JWT token for logging in on next app launch
                SettingsStorageExtensions.SaveString(_localSettings, "Username", username);
                SettingsStorageExtensions.SaveCredentialsToLocker(username, _client.GetToken());
                CurrentUser = await _client.Users.GetCurrentUser();
            }

            return isAuthenticated;
        }

        public async Task<List<CommentThreaded>> GetCommentsForPost(int postid)
        {
            var comments = await _client.Comments.GetAllCommentsForPost(postid);
            return comments.ToThreaded();
        }

        public async Task<IEnumerable<Post>> GetLatestPosts(int page = 0, int perPage = 20)
        {
            page++;

            if(_first20Posts != null && page == 1)
                return _first20Posts;

            var posts = await _client.Posts.Query(new PostsQueryBuilder()
            {
                Page = page,
                PerPage = perPage,
                Embed = true
            });

            if (page == 1)
                _first20Posts = posts;

            return posts;
        }

        public User GetUser()
        {
            return CurrentUser;
        }

        public async Task<bool> IsUserAuthenticated()
        {
            IsLoggedIn = await _client.IsValidJWToken();
            return IsLoggedIn;
        }

        public async Task<Comment> PostComment(int postId, string text)
        {
            return await _client.Comments.Create(new Comment(postId, text));
        }

        public async Task<bool> Logout()
        {
            _client.Logout();
            IsLoggedIn = await _client.IsValidJWToken();
            SettingsStorageExtensions.RemoveCredentialsFromLocker();
            return IsLoggedIn;
        }
    }
}
