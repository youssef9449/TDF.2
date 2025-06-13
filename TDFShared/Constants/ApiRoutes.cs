using System;

namespace TDFShared.Constants
{
    /// <summary>
    /// API route constants
    /// </summary>
    public static class ApiRoutes
    {
        /// <summary>
        /// Base API route
        /// </summary>
        public const string Base = "api";

        /// <summary>
        /// Removes the API base prefix from a route if present
        /// </summary>
        /// <param name="route">The route to process</param>
        /// <returns>The route without the API base prefix</returns>
        public static string RemoveBasePrefix(string route)
        {
            route = route?.TrimStart('/') ?? string.Empty;
            string prefix = $"{Base}/";

            if (route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return route.Substring(prefix.Length);
            }

            return route;
        }

        /// <summary>
        /// API documentation route
        /// </summary>
        public const string Docs = Base + "/docs";

        /// <summary>
        /// Profile routes
        /// </summary>
        public static class Profile
        {
            /// <summary>
            /// Base profile route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/profile";
        }

        /// <summary>
        /// Settings routes
        /// </summary>
        public static class Settings
        {
            /// <summary>
            /// Base settings route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/settings";
        }

        /// <summary>
        /// Documents routes
        /// </summary>
        public static class Documents
        {
            /// <summary>
            /// Base documents route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/documents";
        }

        /// <summary>
        /// Reports routes
        /// </summary>
        public static class Reports
        {
            /// <summary>
            /// Base reports route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/reports";
        }

        /// <summary>
        /// Authentication routes
        /// </summary>
        public static class Auth
        {
            /// <summary>
            /// Base auth route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/auth";

            /// <summary>
            /// Reset password route
            /// </summary>
            public const string ResetPassword = Base + "/reset-password";

            /// <summary>
            /// Login route
            /// </summary>
            public const string Login = Base + "/login";

            /// <summary>
            /// Register route
            /// </summary>
            public const string Register = Base + "/register";

            /// <summary>
            /// Refresh token route
            /// </summary>
            public const string RefreshToken = Base + "/refresh-token";

            /// <summary>
            /// Logout route
            /// </summary>
            public const string Logout = Base + "/logout";

            /// <summary>
            /// Change password route
            /// </summary>
            public const string ChangePassword = ApiRoutes.Base + "/users/change-password";
        }

        /// <summary>
        /// User routes
        /// </summary>
        public static class Users
        {
            /// <summary>
            /// Base users route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/users";

            /// <summary>
            /// Get user by ID route (format with user ID)
            /// </summary>
            public const string GetById = Base + "/{0}";

            /// <summary>
            /// Get user status route (format with user ID)
            /// </summary>
            public const string GetStatus = Base + "/{0}/status";

            /// <summary>
            /// Get online users route
            /// </summary>
            public const string GetOnline = Base + "/online";

            /// <summary>
            /// Get user profile route
            /// </summary>
            public const string GetProfile = Base + "/profile";

            /// <summary>
            /// Get user profile image route
            /// </summary>
            public const string GetProfileImage = Base + "/profile/image";

            /// <summary>
            /// Get user team members route (format with user ID)
            /// </summary>
            public const string GetTeam = Base + "/{0}/team";

            /// <summary>
            /// Get team members for the current user
            /// </summary>
            public const string GetTeamMembers = Base + "/team";

            /// <summary>
            /// Create a new user
            /// </summary>
            public const string Create = Base + "/create";

            /// <summary>
            /// Change password route
            /// </summary>
            public const string ChangePassword = Base + "/change-password";

            /// <summary>
            /// Get users by department route (format with department ID/name)
            /// </summary>
            public const string GetByDepartment = Base + "/department/{0}";

            /// <summary>
            /// Update user route (format with user ID)
            /// </summary>
            public const string Update = Base + "/{0}";

            /// <summary>
            /// Delete user route (format with user ID)
            /// </summary>
            public const string Delete = Base + "/{0}";

            /// <summary>
            /// Update user profile route
            /// </summary>
            public const string UpdateMyProfile = Base + "/profile";

            /// <summary>
            /// Upload user profile picture route
            /// </summary>
            public const string UploadProfilePicture = Base + "/profile/image";

            /// <summary>
            /// Get all users with presence status
            /// </summary>
            public const string GetAllWithStatus = Base + "/all";

            /// <summary>
            /// Get all users route
            /// </summary>
            public const string GetAll = Base;

            /// <summary>
            /// Get current user route
            /// </summary>
            public const string GetCurrent = Base + "/current";

            /// <summary>
            /// Verify user route
            /// </summary>
            public const string Verify = Base + "/verify";

            /// <summary>
            /// Update user connection status route (format with user ID)
            /// </summary>
            public const string UpdateConnection = Base + "/{0}/connection";

            /// <summary>
            /// Base route for user-related endpoints
            /// </summary>
            public static string UsersBase { get; } = "api/users";
        }

        /// <summary>
        /// Request routes
        /// </summary>
        public static class Requests
        {
            /// <summary>
            /// Base requests route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/requests";

            /// <summary>
            /// Get user's own requests route
            /// </summary>
            public const string GetMy = Base + "/my";

            /// <summary>
            /// Get requests by department route (format with department name)
            /// </summary>
            public const string GetByDepartment = Base + "/department/{0}";

            /// <summary>
            /// Get requests by user ID route (format with user ID)
            /// </summary>
            public const string GetByUserId = Base + "/user/{0}";

            /// <summary>
            /// Get request by ID route (format with request ID)
            /// </summary>
            public const string GetById = Base + "/{0}";

            /// <summary>
            /// Approve request route (format with request ID)
            /// </summary>
            public const string Approve = Base + "/{0}/approve";

            /// <summary>
            /// Reject request route (format with request ID)
            /// </summary>
            public const string Reject = Base + "/{0}/reject";

            /// <summary>
            /// Get leave balances route
            /// </summary>
            public const string GetBalances = Base + "/balances";

            /// <summary>
            /// Get user leave balances route (format with user ID)
            /// </summary>
            public const string GetUserBalances = Base + "/balances/{0}";

            /// <summary>
            /// Get all requests route
            /// </summary>
            public const string GetAll = Base;

            /// <summary>
            /// Create request route
            /// </summary>
            public const string Create = Base;

            /// <summary>
            /// Update request route (format with request ID)
            /// </summary>
            public const string Update = Base + "/{0}";

            /// <summary>
            /// Delete request route (format with request ID)
            /// </summary>
            public const string Delete = Base + "/{0}";

            /// <summary>
            /// Get requests for approval route
            /// </summary>
            public const string GetForApproval = Base + "/approval";

            /// <summary>
            /// Get recent requests route
            /// </summary>
            public const string GetRecent = Base + "/recent";

            /// <summary>
            /// Get recent requests for dashboard route
            /// </summary>
            public const string GetRecentDashboard = Base + "/recent-dashboard";

            /// <summary>
            /// Get pending requests count for dashboard route
            /// </summary>
            public const string GetPendingDashboardCount = Base + "/pending/count";

            /// <summary>
            /// Manager approve request route (format with request ID)
            /// </summary>
            public const string ManagerApprove = Base + "/{0}/manager/approve";

            /// <summary>
            /// HR approve request route (format with request ID)
            /// </summary>
            public const string HRApprove = Base + "/{0}/hr/approve";

            /// <summary>
            /// Manager reject request route (format with request ID)
            /// </summary>
            public const string ManagerReject = Base + "/{0}/manager/reject";

            /// <summary>
            /// HR reject request route (format with request ID)
            /// </summary>
            public const string HRReject = Base + "/{0}/hr/reject";

            // Controller route templates (for use in [HttpGet], [HttpPost], etc.)
            /// <summary>
            /// Manager approve request route template for controller
            /// </summary>
            public const string ManagerApproveTemplate = "{id:int}/manager/approve";

            /// <summary>
            /// HR approve request route template for controller
            /// </summary>
            public const string HRApproveTemplate = "{id:int}/hr/approve";

            /// <summary>
            /// Manager reject request route template for controller
            /// </summary>
            public const string ManagerRejectTemplate = "{id:int}/manager/reject";

            /// <summary>
            /// HR reject request route template for controller
            /// </summary>
            public const string HRRejectTemplate = "{id:int}/hr/reject";
        }

        /// <summary>
        /// Message routes
        /// </summary>
        public static class Messages
        {
            /// <summary>
            /// Base messages route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/messages";

            /// <summary>
            /// Chat route
            /// </summary>
            public const string Chat = Base + "/chat";

            /// <summary>
            /// Recent chat messages route
            /// </summary>
            public const string RecentChat = Chat + "/recent";

            /// <summary>
            /// Mark message as read route (format with message ID)
            /// </summary>
            public const string MarkRead = Base + "/{0}/read";

            /// <summary>
            /// Mark multiple messages as read
            /// </summary>
            public const string MarkBulkRead = Base + "/read";

            /// <summary>
            /// Private messages route
            /// </summary>
            public const string Private = Base + "/private";

            /// <summary>
            /// Mark message as delivered route (format with message ID)
            /// </summary>
            public const string MarkDelivered = Base + "/{0}/delivered";

            /// <summary>
            /// Get message by ID route (format with message ID)
            /// </summary>
            public const string GetById = Base + "/{0}";

            /// <summary>
            /// Get messages by user ID route (format with user ID)
            /// </summary>
            public const string GetByUser = Base + "/user/{0}";

            /// <summary>
            /// Mark messages as delivered in bulk
            /// </summary>
            public const string MarkBulkDelivered = Base + "/delivered/bulk";

            /// <summary>
            /// Mark messages from a sender as delivered
            /// </summary>
            public const string MarkFromSenderDelivered = Base + "/delivered/from-sender";

            /// <summary>
            /// Get unread messages count for a user
            /// </summary>
            public const string GetUnreadCount = Base + "/unread/count/{0}";
        }

        /// <summary>
        /// Notification routes
        /// </summary>
        public static class Notifications
        {
            /// <summary>
            /// Base notifications route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/notifications";

            /// <summary>
            /// Get unread notifications route
            /// </summary>
            public const string GetUnread = Base + "/unread";

            /// <summary>
            /// Get unread notifications for user route (format with user ID)
            /// </summary>
            public const string GetUnreadForUser = Base + "/unread/{0}";

            /// <summary>
            /// Mark notification as seen route (format with notification ID)
            /// </summary>
            public const string MarkSeen = Base + "/{0}/seen";

            /// <summary>
            /// Mark all notifications as seen route
            /// </summary>
            public const string MarkAllSeen = Base + "/seen";

            /// <summary>
            /// Broadcast notification route
            /// </summary>
            public const string Broadcast = Base + "/broadcast";

            /// <summary>
            /// Delete notification route (format with notification ID)
            /// </summary>
            public const string Delete = Base + "/{0}";
        }

        /// <summary>
        /// Lookup routes
        /// </summary>
        public static class Lookups
        {
            /// <summary>
            /// Base lookups route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/lookups";

            /// <summary>
            /// Get all lookups route
            /// </summary>
            public const string GetAll = Base + "/all";

            /// <summary>
            /// Get departments route
            /// </summary>
            public const string GetDepartments = Base + "/departments";

            /// <summary>
            /// Get titles by department route (format with department name)
            /// </summary>
            public const string GetTitlesByDepartment = Base + "/titles/{0}";

            /// <summary>
            /// Get leave types route
            /// </summary>
            public const string GetLeaveTypes = Base + "/leave-types";

            /// <summary>
            /// Get request types route
            /// </summary>
            public const string GetRequestTypes = Base + "/requesttypes";

            /// <summary>
            /// Get status codes route
            /// </summary>
            public const string GetStatusCodes = Base + "/status-codes";
        }

        /// <summary>
        /// Health routes
        /// </summary>
        public static class Health
        {
            /// <summary>
            /// Base health route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/healthcheck";

            /// <summary>
            /// Default health check route
            /// </summary>
            public const string GetDefault = Base;

            /// <summary>
            /// Ping route to check API health
            /// </summary>
            public const string Ping = Base + "/ping";

            /// <summary>
            /// Detailed health check route
            /// </summary>
            public const string Detailed = Base + "/detailed";

            /// <summary>
            /// Echo route for testing request/response
            /// </summary>
            public const string Echo = Base + "/echo";
        }

        /// <summary>
        /// Push Token routes
        /// </summary>
        public static class PushToken
        {
            /// <summary>
            /// Base push token route
            /// </summary>
            public const string Base = ApiRoutes.Base + "/pushtoken";

            /// <summary>
            /// Register push token route
            /// </summary>
            public const string Register = Base + "/register";

            /// <summary>
            /// Unregister push token route
            /// </summary>
            public const string Unregister = Base + "/unregister";
        }

        /// <summary>
        /// WebSocket routes
        /// </summary>
        public static class WebSocket
        {
            /// <summary>
            /// Base WebSocket route
            /// </summary>
            public const string Base = "/ws";

            /// <summary>
            /// WebSocket connection route
            /// </summary>
            public const string Connect = Base;

            /// <summary>
            /// WebSocket message types
            /// </summary>
            public static class MessageTypes
            {
                /// <summary>
                /// Chat message type
                /// </summary>
                public const string ChatMessage = "chat_message";

                /// <summary>
                /// Notification message type
                /// </summary>
                public const string Notification = "notification";

                /// <summary>
                /// User presence message type
                /// </summary>
                public const string UserPresence = "user_presence";

                /// <summary>
                /// Message status update type
                /// </summary>
                public const string MessageStatus = "message_status";

                /// <summary>
                /// Error message type
                /// </summary>
                public const string Error = "error";

                /// <summary>
                /// Ping message type
                /// </summary>
                public const string Ping = "ping";

                /// <summary>
                /// Pong message type
                /// </summary>
                public const string Pong = "pong";
            }
        }
    }
}