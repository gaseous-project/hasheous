using System.Data;
using System.Security.Claims;
using System.Text;
using Authentication;
using Classes;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class AccountAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public AccountAdminController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<AccountAdminController>();
        }

        [HttpGet]
        [Route("Users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            List<UserViewModel> users = new List<UserViewModel>();

            foreach (ApplicationUser rawUser in _userManager.Users)
            {
                UserViewModel user = new UserViewModel();
                user.Id = rawUser.Id;
                user.EmailAddress = rawUser.NormalizedEmail.ToLower();
                user.LockoutEnabled = rawUser.LockoutEnabled;
                user.LockoutEnd = rawUser.LockoutEnd;
                user.SecurityProfile = rawUser.SecurityProfile;

                // get roles
                ApplicationUser? aUser = await _userManager.FindByIdAsync(rawUser.Id);
                if (aUser != null)
                {
                    IList<string> aUserRoles = await _userManager.GetRolesAsync(aUser);
                    user.Roles = aUserRoles.ToList();

                    user.Roles.Sort();
                }

                users.Add(user);
            }

            return Ok(users);
        }

        [HttpPost]
        [Route("Users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> NewUser(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser user = new ApplicationUser
                {
                    UserName = model.UserName,
                    NormalizedUserName = model.UserName.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper()
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // add new users to the player role
                    await _userManager.AddToRoleAsync(user, "Player");

                    Logging.Log(Logging.LogType.Information, "User Management", User.FindFirstValue(ClaimTypes.Name) + " created user " + model.Email + " with password.");

                    return Ok(result);
                }
                else
                {
                    return Ok(result);
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet]
        [Route("Users/{UserId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUser(string UserId)
        {
            ApplicationUser? rawUser = await _userManager.FindByIdAsync(UserId);

            if (rawUser != null)
            {
                UserViewModel user = new UserViewModel();
                user.Id = rawUser.Id;
                user.EmailAddress = rawUser.NormalizedEmail.ToLower();
                user.LockoutEnabled = rawUser.LockoutEnabled;
                user.LockoutEnd = rawUser.LockoutEnd;
                user.SecurityProfile = rawUser.SecurityProfile;

                // get roles
                IList<string> aUserRoles = await _userManager.GetRolesAsync(rawUser);
                user.Roles = aUserRoles.ToList();

                user.Roles.Sort();

                return Ok(user);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpDelete]
        [Route("Users/{UserId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string UserId)
        {
            // get user
            ApplicationUser? user = await _userManager.FindByIdAsync(UserId);

            if (user == null)
            {
                return NotFound();
            }
            else
            {
                await _userManager.DeleteAsync(user);
                Logging.Log(Logging.LogType.Information, "User Management", User.FindFirstValue(ClaimTypes.Name) + " deleted user " + user.Email);
                return Ok();
            }
        }

        [HttpPost]
        [Route("Users/{UserId}/Roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetUserRoles(string UserId, string RoleName)
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(UserId);

            if (user != null)
            {
                // get roles
                List<string> userRoles = (await _userManager.GetRolesAsync(user)).ToList();

                // delete all roles
                foreach (string role in userRoles)
                {
                    if ((new string[] { "Admin", "Member" }).Contains(role))
                    {
                        await _userManager.RemoveFromRoleAsync(user, role);
                    }
                }

                // add only requested roles
                switch (RoleName)
                {
                    case "Admin":
                        await _userManager.AddToRoleAsync(user, "Admin");
                        await _userManager.AddToRoleAsync(user, "Moderator");
                        await _userManager.AddToRoleAsync(user, "Member");
                        break;
                    case "Moderator":
                        await _userManager.AddToRoleAsync(user, "Moderator");
                        await _userManager.AddToRoleAsync(user, "Member");
                        break;
                    case "Member":
                        await _userManager.AddToRoleAsync(user, "Member");
                        break;
                    default:
                        await _userManager.AddToRoleAsync(user, RoleName);
                        break;
                }

                return Ok();
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Route("Users/{UserId}/SecurityProfile")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetUserSecurityProfile(string UserId, SecurityProfileViewModel securityProfile)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser? user = await _userManager.FindByIdAsync(UserId);

                if (user != null)
                {
                    user.SecurityProfile = securityProfile;
                    await _userManager.UpdateAsync(user);

                    return Ok();
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Route("Users/{UserId}/Password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(string UserId, SetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // we can reset the users password
                ApplicationUser? user = await _userManager.FindByIdAsync(UserId);
                if (user != null)
                {
                    string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    IdentityResult passwordChangeResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
                    if (passwordChangeResult.Succeeded == true)
                    {
                        return Ok(passwordChangeResult);
                    }
                    else
                    {
                        return Ok(passwordChangeResult);
                    }
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return NotFound();
            }
        }
    }
}