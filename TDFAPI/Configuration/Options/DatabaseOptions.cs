using System;

namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed database configuration bound from the <c>Database</c> section.
    /// If a fully-formed connection string is provided (either via
    /// <see cref="ConnectionString"/> or <c>ConnectionStrings:DefaultConnection</c>
    /// at bind time) it is used verbatim; otherwise a SQL Server connection string
    /// is composed from the individual fields below.
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>Explicit pre-built connection string. Takes precedence when set.</summary>
        public string? ConnectionString { get; set; }

        /// <summary>"namedpipes" (default) or "tcp".</summary>
        public string ConnectionMethod { get; set; } = "namedpipes";

        /// <summary>SQL Server host. "." means local instance via named pipes.</summary>
        public string ServerIP { get; set; } = ".";

        /// <summary>Name of the target database.</summary>
        public string Database { get; set; } = "Users";

        /// <summary>When true, use Windows authentication. When false, use SQL auth with <see cref="User_Id"/>/<see cref="Password"/>.</summary>
        public bool Trusted_Connection { get; set; } = true;

        /// <summary>Whether to trust the server certificate without chain validation.</summary>
        public bool TrustServerCertificate { get; set; } = true;

        /// <summary>SQL auth username. Only used when <see cref="Trusted_Connection"/> is false.</summary>
        public string? User_Id { get; set; }

        /// <summary>SQL auth password. Only used when <see cref="Trusted_Connection"/> is false.</summary>
        public string? Password { get; set; }

        /// <summary>TCP port, only used when <see cref="ConnectionMethod"/> is "tcp".</summary>
        public int Port { get; set; } = 1433;

        /// <summary>
        /// Builds the effective ADO.NET connection string.
        /// Prefers <see cref="ConnectionString"/> when it is non-empty;
        /// otherwise composes one from the individual fields.
        /// </summary>
        public string BuildConnectionString()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString!;
            }

            var method = (ConnectionMethod ?? "namedpipes").Trim().ToLowerInvariant();
            if (method != "namedpipes" && method != "tcp")
            {
                method = "namedpipes";
            }

            var server = string.IsNullOrWhiteSpace(ServerIP) ? "." : ServerIP;
            var database = string.IsNullOrWhiteSpace(Database) ? "Users" : Database;
            var trust = TrustServerCertificate ? "True" : "False";

            if (Trusted_Connection)
            {
                return method == "namedpipes"
                    ? $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate={trust};"
                    : $"Server={server},{Port};Database={database};Trusted_Connection=True;TrustServerCertificate={trust};";
            }

            var userId = string.IsNullOrWhiteSpace(User_Id) ? "sa" : User_Id;
            var password = Password ?? string.Empty;

            return method == "namedpipes"
                ? $"Server={server};Database={database};User Id={userId};Password={password};TrustServerCertificate={trust};"
                : $"Server={server},{Port};Database={database};User Id={userId};Password={password};TrustServerCertificate={trust};";
        }
    }
}
