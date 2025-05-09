using System;
using System.Threading.Tasks;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using TDFShared.Exceptions;

namespace TDFMAUI
{
    public static class PageExtensions
    {
        /// <summary>
        /// Displays a user-friendly error alert for API exceptions
        /// </summary>
        public static async Task DisplayApiErrorAsync(this Page page, Exception ex, string title = "Error")
        {
            // Log the error
            DebugService.LogError("ApiError", ex);
            
            string message;
            if (ex is ApiException apiEx)
            {
                message = ApiService.GetFriendlyErrorMessage(apiEx);
            }
            else
            {
                message = ex.Message;
            }
            
            await page.DisplayAlert(title, message, "OK");
        }
        
        /// <summary>
        /// Executes an API operation and handles errors with a user-friendly message
        /// </summary>
        public static async Task<T> ExecuteApiOperationAsync<T>(this Page page, 
            Func<Task<T>> operation, 
            string errorTitle = "Error",
            T defaultValue = default)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                await page.DisplayApiErrorAsync(ex, errorTitle);
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Executes an API operation that doesn't return a value and handles errors
        /// </summary>
        public static async Task ExecuteApiOperationAsync(this Page page, 
            Func<Task> operation, 
            string errorTitle = "Error")
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                await page.DisplayApiErrorAsync(ex, errorTitle);
            }
        }
    }
} 