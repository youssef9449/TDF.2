using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using TDFShared.Enums;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converter to determine button visibility based on request status, user role, and ownership
    /// </summary>
    public class RequestButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return false;

            // Expected values: [RequestStatus, CurrentUserId, RequestOwnerId, IsCurrentUserAdmin]
            var status = values[0]?.ToString();
            var currentUserId = values[1] as int?;
            var requestOwnerId = values[2] as int?;
            var isCurrentUserAdmin = values[3] as bool?;
            var buttonType = parameter?.ToString();

            if (!currentUserId.HasValue || !requestOwnerId.HasValue || !isCurrentUserAdmin.HasValue)
                return false;

            var isOwner = currentUserId.Value == requestOwnerId.Value;
            var isAdmin = isCurrentUserAdmin.Value;
            var isPending = string.Equals(status, RequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase);

            return buttonType?.ToLower() switch
            {
                "edit" => isPending && isOwner, // Only owner can edit pending requests
                "delete" => isPending && (isOwner || isAdmin), // Owner or admin can delete pending requests
                "approve" => isPending && !isOwner && isAdmin, // Only admin can approve, not the owner
                "reject" => isPending && !isOwner && isAdmin, // Only admin can reject, not the owner
                "details" => true, // Anyone can view details
                _ => false
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Simple converter to check if user can edit/delete (is owner and status is pending)
    /// </summary>
    public class CanEditDeleteConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return false;

            var status = values[0]?.ToString();
            var currentUserId = values[1] as int?;
            var requestOwnerId = values[2] as int?;

            if (!currentUserId.HasValue || !requestOwnerId.HasValue)
                return false;

            var isOwner = currentUserId.Value == requestOwnerId.Value;
            var isPending = string.Equals(status, RequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase);

            return isPending && isOwner;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to check if user can approve/reject (is admin, not owner, and status is pending)
    /// </summary>
    public class CanApproveRejectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return false;

            var status = values[0]?.ToString();
            var currentUserId = values[1] as int?;
            var requestOwnerId = values[2] as int?;
            var isCurrentUserAdmin = values[3] as bool?;

            if (!currentUserId.HasValue || !requestOwnerId.HasValue || !isCurrentUserAdmin.HasValue)
                return false;

            var isOwner = currentUserId.Value == requestOwnerId.Value;
            var isAdmin = isCurrentUserAdmin.Value;
            var isPending = string.Equals(status, RequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase);

            return isPending && !isOwner && isAdmin;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
