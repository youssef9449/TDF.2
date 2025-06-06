using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Messages;
using Microsoft.Extensions.Logging;
using TDFShared.Enums;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using INotificationService = TDFMAUI.Services.INotificationService;
using System.Threading;
using System.Linq;

namespace TDFMAUI.Features.Dashboard
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IAuthService _authService;
        private CancellationTokenSource? _refreshCts;

        [ObservableProperty]
        private string welcomeMessage = "Welcome back!";

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<NotificationDto> recentNotifications = new();

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> recentRequests = new();

        [ObservableProperty]
        private int unreadNotificationsCount;
        [ObservableProperty]
        private int unreadMessagesCount;
        [ObservableProperty]
        private int pendingRequestsCount;

        [ObservableProperty]
        private bool hasRecentNotifications;

        [ObservableProperty]
        private bool _isDataLoaded;

        [ObservableProperty]
        private bool isRefreshing;

        public DashboardViewModel(
            IRequestService requestService,
            INotificationService notificationService,
            ILogger<DashboardViewModel> logger,
            IAuthService authService)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            Title = "Dashboard";

            // Set personalized welcome message if user is available
            if (App.CurrentUser != null)
            {
                // Extract first name from FullName (text before the first space)
                string firstName = App.CurrentUser.FullName?.Split(' ').FirstOrDefault() ?? "User";
                WelcomeMessage = $"Welcome back, {firstName}!";
            }

            // Subscribe to user changed event to update welcome message
            App.UserChanged += OnUserChanged;
        }

        // Default parameterless constructor for design-time support ONLY
        public DashboardViewModel()
        {
            // This constructor is ONLY for design-time support (XAML previews, designers)
            // Production code MUST use the dependency injection constructor above

            if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
            {
                // Design-time initialization only
                Title = "Dashboard";
                WelcomeMessage = "Welcome to TDF!";

                // Initialize collections with sample data for design-time
                RecentNotifications = new ObservableCollection<NotificationDto>();
                RecentRequests = new ObservableCollection<RequestResponseDto>();

                // Add sample data for design-time preview
                RecentNotifications.Add(new NotificationDto
                {
                    NotificationId = 1,
                    Message = "Sample notification",
                    Timestamp = DateTime.Now
                });
                RecentRequests.Add(new RequestResponseDto
                {
                    RequestID = 1,
                    Status = RequestStatus.Pending,
                    CreatedDate = DateTime.Now
                });

                // Set sample stats for design-time
                PendingRequestsCount = 3;
                UnreadNotificationsCount = 2;
                UnreadMessagesCount = 1;
                HasRecentNotifications = true;
                IsDataLoaded = true;
            }
            else
            {
                // Production runtime - this should NEVER be called
                // If this is reached, it indicates a dependency injection configuration error
                throw new InvalidOperationException(
                    "DashboardViewModel parameterless constructor should only be used for design-time support. " +
                    "In production, use dependency injection with the constructor that accepts IRequestService, " +
                    "INotificationService, and ILogger parameters. Check your service registration in MauiProgram.cs.");
            }
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (App.CurrentUser != null)
            {
                // Extract first name from FullName (text before the first space)
                string firstName = App.CurrentUser.FullName?.Split(' ').FirstOrDefault() ?? "User";
                WelcomeMessage = $"Welcome back, {firstName}!";
                RefreshCommand.Execute(null);
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            if (IsBusy) return;

            // Cancel any existing refresh operation
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var ct = _refreshCts.Token;

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                IsBusy = true;
                IsRefreshing = true;
            });

            try
            {
                _logger.LogInformation("Refreshing dashboard data");

                // Update current date
                CurrentDate = DateTime.Now;

                // Fetch all data in parallel for better performance
                var statsTask = LoadStatsAsync(ct);
                var notificationsTask = LoadRecentNotificationsAsync(ct);
                var requestsTask = LoadRecentRequestsAsync(ct);

                await Task.WhenAll(statsTask, notificationsTask, requestsTask);

                // Update UI state flags
                HasRecentNotifications = RecentNotifications.Count > 0;
                IsDataLoaded = true;

                _logger.LogInformation("Dashboard refresh completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Dashboard refresh was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard data");
                await _notificationService.ShowErrorAsync("Failed to refresh dashboard data. Please try again.");
            }
            finally
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                    IsBusy = false;
                    IsRefreshing = false;
                });
            }
        }

        private async Task LoadStatsAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                PendingRequestsCount = await _requestService.GetPendingDashboardRequestCountAsync();

                // Get unread notifications count
                var notifications = await _notificationService.GetUnreadNotificationsAsync();
                if (notifications == null)
                {
                    _logger.LogWarning("LoadStatsAsync: API returned null for notifications");
                    UnreadNotificationsCount = 0;
                }
                else
                {
                    UnreadNotificationsCount = notifications.Count();
                }

                // Get unread messages count
                var unreadMessagesCount = await _notificationService.GetUnreadMessagesCountAsync();
                UnreadMessagesCount = unreadMessagesCount;

                _logger.LogInformation("Stats loaded: {PendingRequests} pending requests, {UnreadNotifications} unread notifications, {UnreadMessages} unread messages",
                    PendingRequestsCount, UnreadNotificationsCount, UnreadMessagesCount);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard stats");
                // Set default values on error
                PendingRequestsCount = 0;
                UnreadNotificationsCount = 0;
                UnreadMessagesCount = 0;
            }
        }

        private async Task LoadRecentRequestsAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var recent = await _requestService.GetRecentDashboardRequestsAsync();
                
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                    RecentRequests.Clear();
                    foreach (var request in recent)
                    {
                        RecentRequests.Add(request);
                    }
                });
                
                _logger.LogInformation("Loaded {Count} recent requests", RecentRequests.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent requests");
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                    RecentRequests.Clear();
                });
            }
        }

        private async Task LoadRecentNotificationsAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot load recent notifications: Current user is null");
                    await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                        RecentNotifications.Clear();
                    });
                    return;
                }

                var notificationEntities = await _notificationService.GetUnreadNotificationsAsync();

                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                    RecentNotifications.Clear();
                    if (notificationEntities != null)
                    {
                        // Convert entities to DTOs and take the 5 most recent
                        var recentNotifications = notificationEntities
                            .OrderByDescending(n => n.Timestamp)
                            .Take(5)
                            .Select(entity => new NotificationDto
                            {
                                NotificationId = entity.NotificationID,
                                UserId = entity.ReceiverID,
                                Message = entity.Message,
                                IsSeen = entity.IsSeen,
                                Timestamp = entity.Timestamp,
                                Title = "Notification", // Default title
                                Level = NotificationLevel.Medium // Default level
                            });

                        foreach (var notification in recentNotifications)
                        {
                            RecentNotifications.Add(notification);
                        }
                    }
                });

                _logger.LogInformation("Loaded {Count} recent notifications", RecentNotifications.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent notifications");
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() => {
                    RecentNotifications.Clear();
                });
            }
        }

        [RelayCommand]
        private async Task ViewAllRequestsAsync()
        {
            try
            {
                _logger.LogInformation("Navigating to all requests page");
                await Shell.Current.GoToAsync("//RequestsPage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to requests page");
                await _notificationService.ShowErrorAsync("Navigation failed. Please try again.");
            }
        }

        [RelayCommand]
        private async Task ViewAllNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("Navigating to all notifications page");
                await Shell.Current.GoToAsync("//NotificationsPage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to notifications page");
                await _notificationService.ShowErrorAsync("Navigation failed. Please try again.");
            }
        }

        [RelayCommand]
        private async Task ViewRequestAsync(int requestId)
        {
            try
            {
                if (requestId <= 0)
                {
                    _logger.LogWarning("Cannot view request: Invalid request ID");
                    return;
                }

                _logger.LogInformation("Navigating to request details page for request {RequestId}", requestId);
                await Shell.Current.GoToAsync($"//RequestDetailsPage?RequestId={requestId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to request details page");
                await _notificationService.ShowErrorAsync("Navigation failed. Please try again.");
            }
        }

        [RelayCommand]
        private async Task MarkNotificationAsReadAsync(int notificationId)
        {
            try
            {
                if (notificationId <= 0)
                {
                    _logger.LogWarning("Cannot mark notification as read: Invalid notification ID");
                    return;
                }

                _logger.LogInformation("Marking notification {NotificationId} as read", notificationId);
                await _notificationService.MarkAsSeenAsync(notificationId);
                await Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                await _notificationService.ShowErrorAsync("Failed to mark notification as read. Please try again.");
            }
        }

        // Clean up event subscriptions and cancellation token
        public void Cleanup()
        {
            App.UserChanged -= OnUserChanged;
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
        }
    }
}