using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using System.Linq;
using TDFMAUI.Helpers;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Notification;
using TDFShared.Enums;

namespace TDFMAUI.Pages
{
    public partial class NotificationsPage : ContentPage
    {
        private readonly INotificationService _notificationService;
        private readonly WebSocketService _webSocketService;
        private readonly ApiService _apiService;
        
        private ObservableCollection<NotificationViewModel> _notifications = new ObservableCollection<NotificationViewModel>();
        private Dictionary<int, string> _userCache = new Dictionary<int, string>();
        
        // Commands
        public ICommand ViewCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        
        public NotificationsPage(
            INotificationService notificationService,
            WebSocketService webSocketService,
            ApiService apiService)
        {
            InitializeComponent();
            
            _notificationService = notificationService;
            _webSocketService = webSocketService;
            _apiService = apiService;
            
            // Listen for real-time notification events
            _webSocketService.NotificationReceived += OnNotificationReceived;
            
            // Initialize commands
            ViewCommand = new Command<int>(OnViewNotification);
            DeleteCommand = new Command<int>(OnDeleteNotification);
            
            // Setup the collection view
            notificationsCollection.ItemsSource = _notifications;
            refreshView.Command = new Command(async () => await LoadNotifications());
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadNotifications();
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Unsubscribe from events
            _webSocketService.NotificationReceived -= OnNotificationReceived;
        }
        
        private async Task LoadNotifications()
        {
            if (App.CurrentUser == null)
            {
                return;
            }
            
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            refreshView.IsRefreshing = true;
            
            try
            {
                var notifications = await _notificationService.GetUnreadNotificationsAsync();
                
                // --- Pre-fetch sender names ---
                var senderIdsToFetch = notifications
                    .Select(n => n.SenderID)
                    .Where(id => id.HasValue && !_userCache.ContainsKey(id.Value))
                    .Distinct()
                    .ToList();

                if (senderIdsToFetch.Any())
                {
                    // **ASSUMPTION**: ApiService has GetUserByIdAsync or similar
                    // In a real app, consider a bulk fetch method if available
                    foreach (var senderId in senderIdsToFetch)
                    {
                        try
                        {
                            var user = await _apiService.GetUserByIdAsync(senderId.Value);
                            if (user != null && !_userCache.ContainsKey(senderId.Value))
                            {
                                // Cache the username or full name depending on what's available/preferred
                                _userCache.Add(senderId.Value, user.Username); 
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error, but don't block loading other notifications
                            System.Diagnostics.Debug.WriteLine($"Error fetching user {senderId.Value}: {ex.Message}");
                            // Optionally add a placeholder to cache to prevent re-fetching on error
                             if (!_userCache.ContainsKey(senderId.Value))
                             {
                                 _userCache.Add(senderId.Value, $"User {senderId.Value}"); 
                             }
                        }
                    }
                }
                // --- End pre-fetch ---

                // Update the UI on the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _notifications.Clear();
                    
                    foreach (var notification in notifications.OrderByDescending(n => n.Timestamp))
                    {
                        _notifications.Add(ConvertToViewModel(notification));
                    }
                    
                    var noNotificationsLabel = this.FindByName<Label>("noNotificationsLabel");
                    if (noNotificationsLabel != null)
                        noNotificationsLabel.IsVisible = _notifications.Count == 0;
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load notifications: {ApiService.GetFriendlyErrorMessage(ex)}", "OK");
                 MainThread.BeginInvokeOnMainThread(() =>
                 {
                    var noNotificationsLabel = this.FindByName<Label>("noNotificationsLabel");
                    if (noNotificationsLabel != null)
                        noNotificationsLabel.IsVisible = _notifications.Count == 0;
                 });
            }
            finally
            {
                var refreshView = this.FindByName<RefreshView>("refreshView");
                var loadingIndicator = this.FindByName<ActivityIndicator>("loadingIndicator");
                
                if (refreshView != null)
                    refreshView.IsRefreshing = false;
                    
                if (loadingIndicator != null)
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                }
            }
        }
        
        private NotificationViewModel ConvertToViewModel(NotificationEntity notification)
        {
            return new NotificationViewModel
            {
                NotificationId = notification.NotificationID,
                Message = notification.MessageText,
                Timestamp = notification.Timestamp,
                TimestampFormatted = FormatTimestamp(notification.Timestamp),
                IsSeen = notification.IsSeen,
                IsNotSeen = !notification.IsSeen,
                SenderId = notification.SenderID,
                SenderName = notification.SenderID.HasValue ? GetSenderName(notification.SenderID.Value) : null,
                HasSender = notification.SenderID.HasValue,
                BackgroundColor = notification.IsSeen ? Colors.White : Color.FromArgb("#E3F2FD")
            };
        }
        
        private string FormatTimestamp(DateTime timestamp)
        {
            var timeAgo = DateTime.Now - timestamp;
            
            if (timeAgo.TotalMinutes < 1)
                return "Just now";
            if (timeAgo.TotalMinutes < 60)
                return $"{(int)timeAgo.TotalMinutes} minutes ago";
            if (timeAgo.TotalHours < 24)
                return $"{(int)timeAgo.TotalHours} hours ago";
            if (timeAgo.TotalDays < 7)
                return $"{(int)timeAgo.TotalDays} days ago";
                
            // Otherwise just return the date
            return timestamp.ToString("MMM dd, yyyy");
        }
        
        private string GetSenderName(int senderId)
        {
            if (_userCache.TryGetValue(senderId, out var name))
            {
                return name;
            }
            // Fallback if caching failed (should ideally not happen often)
            return $"User {senderId}";
        }
        
        private async void OnNotificationReceived(object sender, NotificationEventArgs e)
        {
            // Add the notification to our list if it's not already there
            // Check by NotificationId to prevent duplicates from WebSocket
            bool exists = _notifications.Any(n => n.NotificationId == e.NotificationId);
            
            if (!exists)
            {
                string senderName = e.SenderName;
                
                // --- Resolve Sender Name for new notification ---
                if (e.SenderId.HasValue && string.IsNullOrEmpty(senderName))
                {
                    if (_userCache.TryGetValue(e.SenderId.Value, out var cachedName))
                    {
                        senderName = cachedName;
                    }
                    else
                    {
                        // Not in cache, try to fetch it
                        try
                        {
                            // **ASSUMPTION**: ApiService has GetUserByIdAsync
                            var user = await _apiService.GetUserByIdAsync(e.SenderId.Value);
                            if (user != null)
                            {
                                senderName = user.Username; // Or user.FullName
                                if (!_userCache.ContainsKey(e.SenderId.Value))
                                {
                                    _userCache.Add(e.SenderId.Value, senderName);
                                }
                            }
                            else
                            {
                                senderName = $"User {e.SenderId.Value}"; // Fallback if user not found
                                if (!_userCache.ContainsKey(e.SenderId.Value))
                                {
                                     _userCache.Add(e.SenderId.Value, senderName); // Cache placeholder
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error fetching user {e.SenderId.Value} for WS notification: {ex.Message}");
                            senderName = $"User {e.SenderId.Value}"; // Fallback on error
                            if (!_userCache.ContainsKey(e.SenderId.Value))
                            {
                                 _userCache.Add(e.SenderId.Value, senderName); // Cache placeholder
                            }
                        }
                    }
                }
                 // --- End Resolve Sender Name ---

                // Create a new notification view model using the resolved name
                var newNotification = new NotificationViewModel
                {
                    NotificationId = e.NotificationId,
                    Message = e.Message,
                    Timestamp = e.Timestamp,
                    TimestampFormatted = FormatTimestamp(e.Timestamp),
                    IsSeen = false, // New notifications are unread
                    IsNotSeen = true,
                    SenderId = e.SenderId,
                    SenderName = senderName, // Use the resolved name
                    HasSender = e.SenderId.HasValue,
                    BackgroundColor = Color.FromArgb("#E3F2FD") // Highlight unread
                };
                
                // Add it to the collection on the main thread (insert at top)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _notifications.Insert(0, newNotification);
                    var noNotificationsLabel = this.FindByName<Label>("noNotificationsLabel");
                    if (noNotificationsLabel != null)
                        noNotificationsLabel.IsVisible = false; // Hide empty message if adding first one
                });
                
                // Display a toast notification (already runs on main thread)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ShowToastAsync($"New notification: {e.Message}");
                });
            }
        }
        
        private async Task ShowToastAsync(string message)
        {
            // Use the NotificationToast control instead of manually creating a toast
            await Controls.NotificationToast.ShowToastAsync(
                this,
                "New Notification",
                message,
                NotificationType.Info);
        }
        
        private async void OnViewNotification(int notificationId)
        {
            var notification = _notifications.FirstOrDefault(n => n.NotificationId == notificationId);
            if (notification == null)
                return;
                
            // Mark as seen
            if (notification.IsNotSeen)
            {
                var success = await _notificationService.MarkAsSeenAsync(notificationId);
                if (success)
                {
                    notification.IsSeen = true;
                    notification.IsNotSeen = false;
                    notification.BackgroundColor = Colors.White;
                }
            }
            
            // Display the notification detail
            await DisplayAlert("Notification", notification.Message, "OK");
        }
        
        private async void OnDeleteNotification(int notificationId)
        {
            var notification = _notifications.FirstOrDefault(n => n.NotificationId == notificationId);
            if (notification == null)
                return;

            bool confirm = await DisplayAlert("Confirm Delete", "Are you sure you want to delete this notification?", "Yes", "No");
            if (!confirm)
                return;

            try
            {
                // Call API to delete the notification
                await _apiService.DeleteAsync($"notifications/{notificationId}"); // Endpoint will automatically get the /api/ prefix

                // Remove from UI only after successful API call
                 _notifications.Remove(notification);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete notification: {ApiService.GetFriendlyErrorMessage(ex)}", "OK");
            }
        }
        
        private async void OnMarkAllReadClicked(object sender, EventArgs e)
        {
            if (_notifications.Count == 0 || !_notifications.Any(n => n.IsNotSeen))
                return;
            
            var notificationIds = _notifications.Where(n => n.IsNotSeen).Select(n => n.NotificationId).ToList();
            if (notificationIds.Count == 0)
                return;
                
            var success = await _notificationService.MarkNotificationsAsSeenAsync(notificationIds);
            if (success)
            {
                foreach (var notification in _notifications)
                {
                    if (notification.IsNotSeen)
                    {
                        notification.IsSeen = true;
                        notification.IsNotSeen = false;
                        notification.BackgroundColor = Colors.White;
                    }
                }
            }
        }

        private void OnNotificationSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is NotificationDto selectedNotification)
            {
                // Handle notification selection
                // e.g., await Navigation.PushAsync(new NotificationDetailsPage(selectedNotification));
                ((ListView)sender).SelectedItem = null; // Deselect
            }
        }
    }
    
    public class NotificationViewModel : BindableObject
    {
        public int NotificationId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimestampFormatted { get; set; }
        
        private bool _isSeen;
        public bool IsSeen 
        { 
            get => _isSeen;
            set
            {
                if (_isSeen != value)
                {
                    _isSeen = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotSeen));
                }
            }
        }
        
        private bool _isNotSeen;
        public bool IsNotSeen
        {
            get => _isNotSeen;
            set
            {
                if (_isNotSeen != value)
                {
                    _isNotSeen = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSeen));
                }
            }
        }
        
        public int? SenderId { get; set; }
        public string SenderName { get; set; }
        public bool HasSender { get; set; }
        
        private Color _backgroundColor;
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnPropertyChanged();
                }
            }
        }
    }
} 