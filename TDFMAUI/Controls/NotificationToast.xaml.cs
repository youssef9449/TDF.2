using Microsoft.Maui.Controls;
using System;
using TDFShared.Enums;

namespace TDFMAUI.Controls
{
    public partial class NotificationToast : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(NotificationToast), string.Empty);

        public static readonly BindableProperty MessageProperty =
            BindableProperty.Create(nameof(Message), typeof(string), typeof(NotificationToast), string.Empty);

        public static readonly new BindableProperty BackgroundColorProperty =
            BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(NotificationToast), Colors.White);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(NotificationToast), Colors.Black);

        public static readonly BindableProperty TitleColorProperty =
            BindableProperty.Create(nameof(TitleColor), typeof(Color), typeof(NotificationToast), Colors.Black);

        public static readonly BindableProperty CloseButtonColorProperty =
            BindableProperty.Create(nameof(CloseButtonColor), typeof(Color), typeof(NotificationToast), Application.Current?.Resources["TextSecondaryColor"] as Color ?? Colors.DarkGray);

        public static readonly BindableProperty IconSourceProperty =
            BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(NotificationToast));

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(NotificationToast), false);

        public static readonly BindableProperty ShowCloseButtonProperty =
            BindableProperty.Create(nameof(ShowCloseButton), typeof(bool), typeof(NotificationToast), true);

        public static readonly BindableProperty AutoDismissProperty =
            BindableProperty.Create(nameof(AutoDismiss), typeof(bool), typeof(NotificationToast), true);

        public static readonly BindableProperty DismissDurationProperty =
            BindableProperty.Create(nameof(DismissDuration), typeof(int), typeof(NotificationToast), 5000);

        public static readonly BindableProperty TagProperty =
            BindableProperty.Create(nameof(Tag), typeof(object), typeof(NotificationToast));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the title of the toast notification
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// Gets or sets the message of the toast notification
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// Gets or sets the background color of the toast
        /// </summary>
        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the text color for the message
        /// </summary>
        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the text color for the title
        /// </summary>
        public Color TitleColor
        {
            get => (Color)GetValue(TitleColorProperty);
            set => SetValue(TitleColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the close button color
        /// </summary>
        public Color CloseButtonColor
        {
            get => (Color)GetValue(CloseButtonColorProperty);
            set => SetValue(CloseButtonColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the icon image source
        /// </summary>
        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show an icon
        /// </summary>
        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show a close button
        /// </summary>
        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the toast should auto-dismiss
        /// </summary>
        public bool AutoDismiss
        {
            get => (bool)GetValue(AutoDismissProperty);
            set => SetValue(AutoDismissProperty, value);
        }

        /// <summary>
        /// Gets or sets the duration in milliseconds before auto-dismissing
        /// </summary>
        public int DismissDuration
        {
            get => (int)GetValue(DismissDurationProperty);
            set => SetValue(DismissDurationProperty, value);
        }

        /// <summary>
        /// Gets or sets the tag object for additional data
        /// </summary>
        public object Tag
        {
            get => GetValue(TagProperty);
            set => SetValue(TagProperty, value);
        }

        #endregion

        private TaskCompletionSource<bool> _tcs;
        private bool _isVisible = false;

        public NotificationToast()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show the notification toast with animation
        /// </summary>
        public async Task<bool> ShowAsync()
        {
            if (_isVisible)
                return false;

            _tcs = new TaskCompletionSource<bool>();
            _isVisible = true;

            // Apply notification type colors if not explicitly set
            ApplyNotificationTypeStyling();

            // Show with animation
            await ToastBorder.FadeTo(1, 250, Easing.CubicOut);
            await ToastBorder.TranslateTo(0, 0, 250, Easing.CubicOut);

            // Auto-dismiss if enabled
            if (AutoDismiss)
            {
                _ = Task.Delay(DismissDuration).ContinueWith(async _ =>
                {
                    await DismissAsync();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            return await _tcs.Task;
        }

        /// <summary>
        /// Dismiss the notification toast with animation
        /// </summary>
        public async Task DismissAsync()
        {
            if (!_isVisible)
                return;

            // Hide with animation
            await ToastBorder.FadeTo(0, 250, Easing.CubicIn);
            await ToastBorder.TranslateTo(0, -30, 250, Easing.CubicIn);

            _isVisible = false;
            _tcs?.TrySetResult(true);
        }

        /// <summary>
        /// Handle the close button click
        /// </summary>
        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            await DismissAsync();
        }

        /// <summary>
        /// Configure the toast styling based on notification type
        /// </summary>
        private void ApplyNotificationTypeStyling()
        {
            if (BackgroundColor != Colors.White || TextColor != Colors.Black)
                return; // Colors have been explicitly set

            // Use the Tag property to determine the notification type
            if (Tag is NotificationType notificationType)
            {
                switch (notificationType)
                {
                    case NotificationType.Success:
                        BackgroundColor = Color.FromArgb("#E7F9ED");
                        TextColor = Color.FromArgb("#1F7B4D");
                        TitleColor = Color.FromArgb("#1F7B4D");
                        CloseButtonColor = Color.FromArgb("#1F7B4D");
                        break;

                    case NotificationType.Warning:
                        BackgroundColor = Color.FromArgb("#FFF8E6");
                        TextColor = Color.FromArgb("#B76E00");
                        TitleColor = Color.FromArgb("#B76E00");
                        CloseButtonColor = Color.FromArgb("#B76E00");
                        break;

                    case NotificationType.Error:
                        BackgroundColor = Color.FromArgb("#FEECEB");
                        TextColor = Color.FromArgb("#B42318");
                        TitleColor = Color.FromArgb("#B42318");
                        CloseButtonColor = Color.FromArgb("#B42318");
                        break;

                    case NotificationType.Info:
                    default:
                        BackgroundColor = Color.FromArgb("#EFF8FF");
                        TextColor = Color.FromArgb("#175CD3");
                        TitleColor = Color.FromArgb("#175CD3");
                        CloseButtonColor = Color.FromArgb("#175CD3");
                        break;
                }
            }
        }

        /// <summary>
        /// Show a toast notification with the given title and message
        /// </summary>
        /// <param name="title">Title of the notification</param>
        /// <param name="message">Message content</param>
        /// <param name="notificationType">Type of notification</param>
        /// <returns>A task that completes when the notification is dismissed</returns>
        public static async Task ShowToastAsync(
            ContentPage page,
            string title,
            string message,
            NotificationType notificationType = NotificationType.Info)
        {
            var toast = new NotificationToast
            {
                Title = title,
                Message = message,
                Tag = notificationType
            };

            // Add to the page's content
            if (page.Content is Grid grid)
            {
                grid.Children.Add(toast);
                Grid.SetRow(toast, 0);
                Grid.SetColumnSpan(toast, grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1);

                // Set ZIndex property directly on the element
                toast.ZIndex = 999;
            }
            else
            {
                // Create an overlay grid if the page doesn't have a grid layout
                var content = page.Content;
                page.Content = null;

                var overlayGrid = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Star }
                    }
                };

                overlayGrid.Children.Add(content);
                Grid.SetRow(content, 1);

                overlayGrid.Children.Add(toast);
                Grid.SetRow(toast, 0);

                page.Content = overlayGrid;
            }

            await toast.ShowAsync();
        }
    }
}