using System.ComponentModel.DataAnnotations;

namespace SAProject.Models
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool RequireMfa { get; set; }
        public DateTime? MfaEnabledAt { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LastFailedLoginDate { get; set; }
        public bool ForceMfaAfterFailedAttempts { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public class UserDetailViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool RequireMfa { get; set; }
        public DateTime? MfaEnabledAt { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LastFailedLoginDate { get; set; }
        public bool ForceMfaAfterFailedAttempts { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public List<string> AllRoles { get; set; } = new List<string>();
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Xác thực email")]
        public bool EmailConfirmed { get; set; } = true;

        [Display(Name = "Yêu cầu MFA")]
        public bool RequireMfa { get; set; }

        [Display(Name = "Bắt buộc MFA sau khi đăng nhập thất bại")]
        public bool ForceMfaAfterFailedAttempts { get; set; } = true;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Display(Name = "Xác thực email")]
        public bool EmailConfirmed { get; set; }

        [Display(Name = "Yêu cầu MFA")]
        public bool RequireMfa { get; set; }

        [Display(Name = "Bắt buộc MFA sau khi đăng nhập thất bại")]
        public bool ForceMfaAfterFailedAttempts { get; set; }
    }

    public class ManageRolesViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public IList<string> UserRoles { get; set; } = new List<string>();
        public List<RoleViewModel> AllRoles { get; set; } = new List<RoleViewModel>();
    }

    public class RoleViewModel
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsSelected { get; set; }
    }
}