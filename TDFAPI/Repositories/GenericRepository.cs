using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TDFAPI.Data;

namespace TDFAPI.Repositories
{
    /// <summary>
    /// Generic repository implementation using Entity Framework Core
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public class GenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger _logger;

        public GenericRepository(ApplicationDbContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all entities
        /// </summary>
        /// <returns>All entities</returns>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        /// <summary>
        /// Gets entities based on filter expression
        /// </summary>
        /// <param name="filter">Filter expression</param>
        /// <returns>Filtered entities</returns>
        public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter)
        {
            return await _dbSet.Where(filter).ToListAsync();
        }

        /// <summary>
        /// Gets a single entity by id
        /// </summary>
        /// <param name="id">Entity id</param>
        /// <returns>Entity if found, null otherwise</returns>
        public virtual async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// Gets first entity that matches the filter
        /// </summary>
        /// <param name="filter">Filter expression</param>
        /// <returns>Entity if found, null otherwise</returns>
        public virtual async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> filter)
        {
            return await _dbSet.FirstOrDefaultAsync(filter);
        }

        /// <summary>
        /// Adds a new entity
        /// </summary>
        /// <param name="entity">Entity to add</param>
        /// <returns>True if added successfully</returns>
        public virtual async Task<bool> AddAsync(T entity)
        {
            try
            {
                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity {EntityType}", typeof(T).Name);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        /// <param name="entity">Entity to update</param>
        /// <returns>True if updated successfully</returns>
        public virtual async Task<bool> UpdateAsync(T entity)
        {
            try
            {
                _dbSet.Attach(entity);
                _context.Entry(entity).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity {EntityType}", typeof(T).Name);
                return false;
            }
        }

        /// <summary>
        /// Deletes an entity
        /// </summary>
        /// <param name="entity">Entity to delete</param>
        /// <returns>True if deleted successfully</returns>
        public virtual async Task<bool> DeleteAsync(T entity)
        {
            try
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity {EntityType}", typeof(T).Name);
                return false;
            }
        }

        /// <summary>
        /// Deletes an entity by id
        /// </summary>
        /// <param name="id">Id of entity to delete</param>
        /// <returns>True if deleted successfully</returns>
        public virtual async Task<bool> DeleteByIdAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                return false;
            }
            return await DeleteAsync(entity);
        }

        /// <summary>
        /// Gets paged results
        /// </summary>
        /// <param name="filter">Filter expression</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="orderBy">Order by expression</param>
        /// <param name="isAscending">Whether to order ascending or descending</param>
        /// <returns>Paged results</returns>
        public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>>? filter = null,
            int pageNumber = 1,
            int pageSize = 10,
            Expression<Func<T, object>>? orderBy = null,
            bool isAscending = true)
        {
            IQueryable<T> query = _dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            var totalCount = await query.CountAsync();

            if (orderBy != null)
            {
                query = isAscending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Checks if any entity matching the filter exists
        /// </summary>
        /// <param name="filter">Filter expression</param>
        /// <returns>True if matching entity exists</returns>
        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> filter)
        {
            return await _dbSet.AnyAsync(filter);
        }

        /// <summary>
        /// Gets total count of entities matching the filter
        /// </summary>
        /// <param name="filter">Filter expression</param>
        /// <returns>Count of matching entities</returns>
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? filter = null)
        {
            return filter == null
                ? await _dbSet.CountAsync()
                : await _dbSet.CountAsync(filter);
        }
    }
} 