using System.Threading.Tasks;

namespace TDFMAUI.Services
{
    public interface INavigationService
    {
        Task NavigateToAsync(string route);
    }
} 