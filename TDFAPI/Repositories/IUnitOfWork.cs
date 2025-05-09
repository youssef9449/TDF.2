using System;
using System.Threading.Tasks;

namespace TDFAPI.Repositories
{
    /// <summary>
    /// Interface for the Unit of Work pattern implementation
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Begins a new transaction
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commits the current transaction
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back the current transaction
        /// </summary>
        Task RollbackAsync();

        /// <summary>
        /// Saves changes to the database without committing the transaction
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Creates a repository for the specified entity type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>A repository for the entity</returns>
        GenericRepository<T> GetRepository<T>() where T : class;
    }
} 