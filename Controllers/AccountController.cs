using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAProject.Data;
using SAProject.Models;
using SAProject.Services;
using SAProject.ViewModels;

namespace SAProject.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IMfaService _mfaService;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IMfaService mfaService,
        IAuditService auditService,
        IEmailService emailService,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _mfaService = mfaService;
        _auditService = auditService;
        _emailService = emailService;
        _roleManager = roleManager;
    }
    #region Auth
    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Audit log
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Register Success",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                );

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                var errorMessage = error.Code switch
                {
                    "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt",
                    "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường ('a'-'z')",
                    "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa ('A'-'Z')",
                    "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số ('0'-'9')",
                    "PasswordTooShort" => $"Mật khẩu phải có ít nhất {_userManager.Options.Password.RequiredLength} ký tự",
                    "DuplicateUserName" => "Email này đã được sử dụng",
                    "DuplicateEmail" => "Email này đã được sử dụng",
                    _ => error.Description
                };

                ModelState.AddModelError(string.Empty, errorMessage);
            }
        }

        return View(model);
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            // Tìm user trước khi đăng nhập
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                // Kiểm tra password một lần duy nhất
                var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);

                if (passwordValid)
                {
                    // RESET failed login count khi login thành công
                    await ResetFailedLoginCountAsync(user);

                    // Kiểm tra nếu user cần MFA (do đã từng login sai nhiều lần trước đó)
                    bool requiresMfa = await CheckIfMfaRequiredAsync(user);

                    if (requiresMfa)
                    {
                        // Chuyển hướng đến trang MFA
                        return await HandleMfaLogin(user, model.RememberMe, returnUrl);
                    }
                    else
                    {
                        // Đăng nhập bình thường không cần MFA
                        var result = await _signInManager.PasswordSignInAsync(
                            model.Email,
                            model.Password,
                            model.RememberMe,
                            lockoutOnFailure: true);

                        if (result.Succeeded)
                        {
                            // Audit log
                            await _auditService.LogAsync(
                                userId: user.Id,
                                userName: user.UserName,
                                action: "Login Success",
                                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                            );

                            // Tránh tấn công Open Redirect
                            if (!Url.IsLocalUrl(returnUrl))
                            {
                                return RedirectToAction("Index", "Home");
                            }
                            return Redirect(returnUrl);
                        }

                        // Xử lý các trường hợp lỗi
                        if (result.IsLockedOut)
                        {
                            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
                        }
                        else if (result.IsNotAllowed)
                        {
                            ModelState.AddModelError(string.Empty, "Bạn chưa được phép đăng nhập.");
                        }
                    }
                }
                else
                {
                    // Password không đúng - TĂNG failed login count
                    await IncrementFailedLoginCountAsync(user);

                    // Kiểm tra nếu đã đạt 5 lần sai
                    bool requiresMfa = await CheckIfMfaRequiredAsync(user);

                    if (requiresMfa && user.FailedLoginCount >= 5)
                    {
                        // Gửi MFA token và chuyển hướng đến trang MFA
                        return await HandleMfaLogin(user, model.RememberMe, returnUrl);
                    }
                    else
                    {
                        // Hiển thị thông báo lỗi bình thường
                        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");

                        // Hiển thị số lần còn lại trước khi phải dùng MFA
                        int remainingAttempts = 5 - user.FailedLoginCount;
                        if (remainingAttempts > 0 && remainingAttempts <= 3)
                        {
                            ModelState.AddModelError(string.Empty, $"Bạn còn {remainingAttempts} lần thử trước khi yêu cầu xác thực bổ sung.");
                        }
                    }
                }
            }
            else
            {
                // User không tồn tại - vẫn hiển thị thông báo chung để tránh enumeration attack
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            }
        }

        return View(model);
    }
    // Kiểm tra xem user có cần MFA không (dựa trên số lần login sai)
    private async Task<bool> CheckIfMfaRequiredAsync(ApplicationUser user)
    {
        // Nếu user đã bật MFA bắt buộc
        if (user.RequireMfa)
            return true;

        // Kiểm tra nếu đã login sai 5 lần và tính năng được bật
        if (user.ForceMfaAfterFailedAttempts && user.FailedLoginCount >= 5)
            return true;

        return false;
    }

    // Tăng số lần login sai
    private async Task IncrementFailedLoginCountAsync(ApplicationUser user)
    {
        if (user != null)
        {
            user.FailedLoginCount++;
            user.LastFailedLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Audit log cho failed attempt
            await _auditService.LogAsync(
                userId: user.Id,
                userName: user.UserName,
                action: $"Login Failed - Attempt {user.FailedLoginCount}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
            );
        }
    }

    // Reset số lần login sai khi login thành công
    private async Task ResetFailedLoginCountAsync(ApplicationUser user)
    {
        if (user != null && user.FailedLoginCount > 0)
        {
            user.FailedLoginCount = 0;
            user.LastFailedLoginDate = null;
            await _userManager.UpdateAsync(user);
        }
    }
    
    private async Task<IActionResult> HandleMfaLogin(ApplicationUser user, bool rememberMe, string returnUrl)
    {
        // Tạo và gửi mã MFA
        var token = await _mfaService.GenerateTokenAsync(user.Id);

        // Lưu thông tin user vào Session
        HttpContext.Session.SetString("MfaUserId", user.Id);
        HttpContext.Session.SetString("MfaRememberMe", rememberMe.ToString());
        HttpContext.Session.SetString("MfaReturnUrl", returnUrl ?? "");

        string actionMessage = user.FailedLoginCount >= 5 ?
            "MFA Required - Too many failed attempts" :
            "MFA Required - Token Sent";

        // Audit log
        await _auditService.LogAsync(
            userId: user.Id,
            userName: user.UserName,
            action: actionMessage,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
        );

        // Thêm thông báo cho user
        TempData["MfaReason"] = user.FailedLoginCount >= 5 ?
            "Để bảo vệ tài khoản của bạn, chúng tôi yêu cầu xác thực bổ sung sau nhiều lần đăng nhập không thành công." :
            "Để tăng cường bảo mật, vui lòng xác thực đăng nhập của bạn.";

        return RedirectToAction("LoginWithMfa", "Mfa");
    }
    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            await _auditService.LogAsync(
                userId: user.Id,
                userName: user.UserName,
                action: "Logout",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
            );
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
    #endregion Auth
    // GET: Account/Index
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .Select(u => new UserViewModel
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                FullName = u.FullName,
                EmailConfirmed = u.EmailConfirmed,
                RequireMfa = u.RequireMfa,
                MfaEnabledAt = u.MfaEnabledAt,
                FailedLoginCount = u.FailedLoginCount,
                LastFailedLoginDate = u.LastFailedLoginDate,
                ForceMfaAfterFailedAttempts = u.ForceMfaAfterFailedAttempts
            })
            .ToListAsync();

        // Thêm role cho từng user
        foreach (var user in users)
        {
            var appUser = await _userManager.FindByIdAsync(user.Id);
            user.Roles = await _userManager.GetRolesAsync(appUser);
        }

        return View(users);
    }

    // GET: Account/Details/5
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.ToListAsync();

        var model = new UserDetailViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            EmailConfirmed = user.EmailConfirmed,
            RequireMfa = user.RequireMfa,
            MfaEnabledAt = user.MfaEnabledAt,
            FailedLoginCount = user.FailedLoginCount,
            LastFailedLoginDate = user.LastFailedLoginDate,
            ForceMfaAfterFailedAttempts = user.ForceMfaAfterFailedAttempts,
            Roles = userRoles,
            AllRoles = allRoles.Select(r => r.Name).ToList()
        };

        return View(model);
    }

    // GET: Account/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Account/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.UserName,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = model.EmailConfirmed,
                RequireMfa = model.RequireMfa,
                ForceMfaAfterFailedAttempts = model.ForceMfaAfterFailedAttempts
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Tạo tài khoản thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    // GET: Account/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            EmailConfirmed = user.EmailConfirmed,
            RequireMfa = user.RequireMfa,
            ForceMfaAfterFailedAttempts = user.ForceMfaAfterFailedAttempts
        };

        return View(model);
    }

    // POST: Account/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditUserViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.UserName = model.UserName;
            user.Email = model.Email;
            user.FullName = model.FullName;
            user.EmailConfirmed = model.EmailConfirmed;
            user.RequireMfa = model.RequireMfa;
            user.ForceMfaAfterFailedAttempts = model.ForceMfaAfterFailedAttempts;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật tài khoản thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    // GET: Account/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new UserViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            EmailConfirmed = user.EmailConfirmed,
            RequireMfa = user.RequireMfa,
            MfaEnabledAt = user.MfaEnabledAt,
            FailedLoginCount = user.FailedLoginCount
        };

        return View(model);
    }

    // POST: Account/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Xóa tài khoản thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = "Xóa tài khoản thất bại!";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: Account/ManageRoles/5
    public async Task<IActionResult> ManageRoles(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.ToListAsync();

        var model = new ManageRolesViewModel
        {
            UserId = user.Id,
            UserName = user.UserName,
            UserRoles = userRoles,
            AllRoles = allRoles.Select(r => new RoleViewModel
            {
                RoleId = r.Id,
                RoleName = r.Name,
                IsSelected = userRoles.Contains(r.Name)
            }).ToList()
        };

        return View(model);
    }

    // POST: Account/ManageRoles/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageRoles(string id, ManageRolesViewModel model)
    {
        if (id != model.UserId)
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        // Xóa các role cũ
        var removeResult = await _userManager.RemoveFromRolesAsync(user, userRoles);
        if (!removeResult.Succeeded)
        {
            TempData["ErrorMessage"] = "Có lỗi khi cập nhật vai trò!";
            return View(model);
        }

        // Thêm các role mới được chọn
        var selectedRoles = model.AllRoles.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();
        var addResult = await _userManager.AddToRolesAsync(user, selectedRoles);

        if (addResult.Succeeded)
        {
            TempData["SuccessMessage"] = "Cập nhật vai trò thành công!";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = "Có lỗi khi cập nhật vai trò!";
        return View(model);
    }

    // POST: Account/ResetPassword/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(newPassword))
        {
            TempData["ErrorMessage"] = "Thông tin không hợp lệ!";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Reset password
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = "Đặt lại mật khẩu thất bại!";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }
}