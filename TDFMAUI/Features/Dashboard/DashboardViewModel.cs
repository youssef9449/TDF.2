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

namespace TDFMAUI.Features.Dashboard
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DashboardViewModel> _logger;


        [ObservableProperty]
        private string welcomeMessage = "Welcome back!";

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> recentRequests = new();

        [ObservableProperty]
        private ObservableCollection<NotificationDto> recentNotifications = new();

        [ObservableProperty]
        private int unreadNotificationsCount;
        [ObservableProperty]
        private int unreadMessagesCount;
        [ObservableProperty]
        private int pendingRequestsCount;

        [ObservableProperty]
        private bool hasRecentNotifications;

        [ObservableProperty]
        private bool hasRecentRequests;

        [ObservableProperty]
        private bool _isDataLoaded;

        [ObservableProperty]
        private bool isRefreshing;

        public DashboardViewModel(
            IRequestService requestService,
            INotificationService notificationService,
            ILogger<DashboardViewModel> logger)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            // Initialize with a refresh
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
                RecentRequests = new ObservableCollection<RequestResponseDto>();
                RecentNotifications = new ObservableCollection<NotificationDto>();

                // Add sample data for design-time preview
                RecentRequests.Add(new RequestResponseDto
                {
                    RequestID = 1,
                    LeaveType = TDFShared.Enums.LeaveType.Annual,
                    RequestStartDate = DateTime.Today.AddDays(7),
                    Status = TDFShared.Enums.RequestStatus.Pending
                });

                RecentNotifications.Add(new NotificationDto
                {
                    NotificationId = 1,
                    Message = "Sample notification",
                    Timestamp = DateTime.Now
                });

                // Set sample stats for design-time
                PendingRequestsCount = 3;
                UnreadNotificationsCount = 2;
                UnreadMessagesCount = 1;
                HasRecentRequests = true;
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

            IsBusy = true;
            IsRefreshing = true;

            try
            {
                _logger.LogInformation("Refreshing dashboard data");

                // Update current date
                CurrentDate = DateTime.Now;

                // Fetch all data in parallel for better performance
                var statsTask = LoadStatsAsync();
                var requestsTask = LoadRecentRequestsAsync();
                var notificationsTask = LoadRecentNotificationsAsync();

                await Task.WhenAll(statsTask, requestsTask, notificationsTask);

                // Update UI state flags
                HasRecentRequests = RecentRequests.Count > 0;
                HasRecentNotifications = RecentNotifications.Count > 0;
                IsDataLoaded = true;

                _logger.LogInformation("Dashboard refresh completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard data");
                await _notificationService.ShowErrorAsync("Failed to refresh dashboard data. Please try again.");
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot load stats: Current user is null");
                    return;
                }

                // Get pending requests count
                var pendingPagination = new RequestPaginationDto
                {
                    PageSize = 1,
                    FilterStatus = RequestStatus.Pending, // Corrected property name
                    CountOnly = true
                };
                if (App.CurrentUser != null) { // Add safety check, though outer method already has one
                    pendingPagination.UserId = App.CurrentUser.UserID;
                }
                var pendingResult = await _requestService.GetAllRequestsAsync(pendingPagination);
                PendingRequestsCount = pendingResult?.Data?.TotalCount ?? 0;

                // Get unread notifications count
                var notifications = await _notificationService.GetUnreadNotificationsAsync();
                UnreadNotificationsCount = notifications?.Count() ?? 0;

                // Get unread messages count
                UnreadMessagesCount = await _notificationService.GetUnreadMessagesCountAsync();

                _logger.LogInformation("Stats loaded: {PendingRequests} pending requests, {UnreadNotifications} unread notifications, {UnreadMessages} unread messages",
                    PendingRequestsCount, UnreadNotificationsCount, UnreadMessagesCount);
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

        private async Task LoadRecentRequestsAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot load recent requests: Current user is null");
                    RecentRequests.Clear();
                    return;
                }

                var pagination = new RequestPaginationDto
                {
                    PageSize = 5,
                    SortBy = "CreatedDate",
                    Ascending = false // Corrected property name and value type
                };

                if (App.CurrentUser != null) { // Add safety check
                    pagination.UserId = App.CurrentUser.UserID;
                }
                var recentResult = await _requestService.GetAllRequestsAsync(pagination);

                RecentRequests.Clear();
                if (recentResult?.Data?.Items != null)
                {
                    foreach (var req in recentResult.Data.Items)
                    {
                        RecentRequests.Add(req);
                    }
                    _logger.LogInformation("Loaded {Count} recent requests", RecentRequests.Count);
                }
                else
                {
                    _logger.LogWarning("No recent requests found or API returned null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent requests");
                RecentRequests.Clear();
            }
        }

        private async Task LoadRecentNotificationsAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot load recent notifications: Current user is null");
                    RecentNotifications.Clear();
                    return;
                }

                var notificationEntities = await _notificationService.GetUnreadNotificationsAsync();

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

                    _logger.LogInformation("Loaded {Count} recent notifications", RecentNotifications.Count);
                }
                else
                {
                    _logger.LogWarning("No recent notifications found or API returned null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent notifications");
                RecentNotifications.Clear();
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
        private async Task ViewRequestAsync(Guid requestId)
        {
            try
            {
                if (requestId == Guid.Empty)
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
                bool success = await _notificationService.MarkAsSeenAsync(notificationId);

                if (success)
                {
                    // Remove from the collection or update its status
                    var notification = RecentNotifications.FirstOrDefault(n => n.NotificationId == notificationId);
                    if (notification != null)
                    {
                        RecentNotifications.Remove(notification);

                        // Update the unread count
                        UnreadNotificationsCount = Math.Max(0, UnreadNotificationsCount - 1);

                        // Update UI state
                        HasRecentNotifications = RecentNotifications.Count > 0;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to mark notification {NotificationId} as read", notificationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                await _notificationService.ShowErrorAsync("Failed to update notification. Please try again.");
            }
        }

        // Clean up event subscriptions
        public void Cleanup()
        {
            App.UserChanged -= OnUserChanged;
        }
    }
}