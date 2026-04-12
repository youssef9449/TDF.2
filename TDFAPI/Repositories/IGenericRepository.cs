using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace TDFAPI.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter);
        Task<T?> GetByIdAsync(object id);
        Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> filter);
        Task<bool> AddAsync(T entity);
        Task<bool> UpdateAsync(T entity);
        Task<bool> DeleteAsync(T entity);
        Task<bool> DeleteByIdAsync(object id);
        Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>>? filter = null,
            int pageNumber = 1,
            int pageSize = 10,
            Expression<Func<T, object>>? orderBy = null,
            bool isAscending = true);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> filter);
        Task<int> CountAsync(Expression<Func<T, bool>>? filter = null);
    }
}
