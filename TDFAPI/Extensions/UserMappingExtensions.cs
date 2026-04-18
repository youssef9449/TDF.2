using TDFShared.DTOs.Users;
using TDFShared.Models.User;
using TDFShared.Services;
using System.Collections.Generic;
using System.Linq;

namespace TDFAPI.Extensions
{
    public static class UserMappingExtensions
    {
        public static UserDto ToDtoWithRoles(this UserEntity entity, IRoleService roleService)
        {
            if (entity == null) return null!;
            var dto = entity.ToDto();
            roleService.AssignRoles(dto);
            return dto;
        }
    }
}
