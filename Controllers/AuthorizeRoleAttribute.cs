using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace ThuYBinhDuongAPI.Controllers
{
    public class AuthorizeRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly int[] _roles;

        public AuthorizeRoleAttribute(params int[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Kiểm tra user có được authenticate không
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Lấy role từ claims
            var roleClaim = context.HttpContext.User.FindFirst("Role")?.Value;
            if (roleClaim == null || !int.TryParse(roleClaim, out int userRole))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Kiểm tra role có trong danh sách được phép không
            if (!_roles.Contains(userRole))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
} 