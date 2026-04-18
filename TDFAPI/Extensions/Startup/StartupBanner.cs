using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Decorative ASCII banners printed to stdout at application start.
    /// Kept in one place so Program.cs stays focused on wiring.
    /// </summary>
    public static class StartupBanner
    {
        public static void WriteStarting(IHostEnvironment environment)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(" --- TDF API Server v1.0 - Initializing... ---");
            Console.WriteLine($" --- Environment: {environment.EnvironmentName} ---");
            Console.WriteLine($" --- Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            Console.WriteLine();
            Console.ResetColor();
        }

        public static void WriteStarted(IHostEnvironment environment, IReadOnlyCollection<string> urls)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(@" _____ ___  _____    _    ___ ___ ");
            Console.WriteLine(@"|_   _|   \|  ___|  /_\  | _ \_ _|");
            Console.WriteLine(@"  | | | |) |  _|   / _ \ |  _/| | ");
            Console.WriteLine(@"  |_| |___/|_|    /_/ \_\|_| |___|");
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"   API Server v1.0 - Started Successfully   ");
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"* Environment: {environment.EnvironmentName}");
            Console.WriteLine($"* URLs: {string.Join(", ", urls)}");
            Console.WriteLine($"* Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            Console.WriteLine();
            Console.ResetColor();
        }
    }
}
