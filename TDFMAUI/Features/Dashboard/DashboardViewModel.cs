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
    public partial class DashboardViewModel : BaseViewModel, IDisposable
    {
        private readonly IRequestService _requestService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IAuthService _authService;
        private CancellationTokenSource? _refreshCts;
        private bool _disposed = false;

        [ObservableProperty]
        private string _welcomeMessage = "Welcome back!";

        [ObservableProperty]
        private DateTime _currentDate = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<NotificationDto> _recentNotifications = new();

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _recentRequests = new();

        [ObservableProperty]
        private int _unreadNotificationsCount;

        [ObservableProperty]
        private int _unreadMessagesCount;

        [ObservableProperty]
        private int _pendingRequestsCount;

        [ObservableProperty]
        private bool _hasRecentNotifications;

        [ObservableProperty]
        private bool _isDataLoaded;

        [ObservableProperty]
        private bool _isRefreshing;

        public DashboardViewModel(
            IRequestService requestService,
            INotificationService notificationService,
            ILogger<DashboardViewModel> logger,
            IAuthService authService)
        {
            _requestService = requestService;
            _notificationService = notificationService;
            _logger = logger;
            _authService = authService;

            Title = "Dashboard";

            UpdateWelcomeMessage();
            App.UserChanged += OnUserChanged;
        }

        private void UpdateWelcomeMessage()
        {
            if (App.CurrentUser != null)
            {
                string firstName = App.CurrentUser.FullName?.Split(' ').FirstOrDefault() ?? "User";
                WelcomeMessage = $"Welcome back, {firstName}!";
            }
        }

        private void OnUserChanged(object? sender, EventArgs e)
        {
            UpdateWelcomeMessage();
            _ = RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (IsBusy) return;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            var ct = _refreshCts.Token;

            IsBusy = true;
            IsRefreshing = true;
            try
            {
                CurrentDate = DateTime.Now;

                await Task.WhenAll(
                    LoadStatsAsync(ct),
                    LoadRecentNotificationsAsync(ct),
                    LoadRecentRequestsAsync(ct)
                );

                HasRecentNotifications = RecentNotifications.Any();
                IsDataLoaded = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard");
                ErrorMessage = "Failed to refresh dashboard data.";
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        private async Task LoadStatsAsync(CancellationToken ct)
        {
            try
            {
                PendingRequestsCount = await _requestService.GetPendingDashboardRequestCountAsync();
                var notifications = await _notificationService.GetUnreadNotificationsAsync();
                UnreadNotificationsCount = notifications?.Count() ?? 0;
                UnreadMessagesCount = await _notificationService.GetUnreadMessagesCountAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading stats"); }
        }

        private async Task LoadRecentRequestsAsync(CancellationToken ct)
        {
            try
            {
                var recent = await _requestService.GetRecentDashboardRequestsAsync();
                await MainThread.InvokeOnMainThreadAsync(() => {
                    RecentRequests.Clear();
                    foreach (var request in recent) RecentRequests.Add(request);
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading recent requests"); }
        }

        private async Task LoadRecentNotificationsAsync(CancellationToken ct)
        {
            try
            {
                var notificationEntities = await _notificationService.GetUnreadNotificationsAsync();
                await MainThread.InvokeOnMainThreadAsync(() => {
                    RecentNotifications.Clear();
                    if (notificationEntities != null)
                    {
                        var recent = notificationEntities
                            .OrderByDescending(n => n.Timestamp)
                            .Take(5)
                            .Select(entity => new NotificationDto
                            {
                                NotificationId = entity.NotificationID,
                                Message = entity.Message,
                                IsSeen = entity.IsSeen,
                                Timestamp = entity.Timestamp
                            });
                        foreach (var n in recent) RecentNotifications.Add(n);
                    }
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading notifications"); }
        }

        [RelayCommand]
        private async Task ViewAllRequestsAsync() => await Shell.Current.GoToAsync("//RequestsPage");

        [RelayCommand]
        private async Task ViewAllNotificationsAsync() => await Shell.Current.GoToAsync("//NotificationsPage");

        [RelayCommand]
        private async Task ViewRequestAsync(int requestId)
        {
            if (requestId > 0) await Shell.Current.GoToAsync($"//RequestDetailsPage?RequestId={requestId}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            App.UserChanged -= OnUserChanged;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
