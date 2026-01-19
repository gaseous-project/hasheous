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
        private readonly RoleManager<ApplicationRole> _roleManager;

        public AccountAdminController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory,
            RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<AccountAdminController>();
            _roleManager = roleManager;
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

                // make sure this user is not the logged in user - this is to prevent an admin from removing their own admin rights
                if (user.EmailAddress == User.FindFirstValue(ClaimTypes.Email).ToLower())
                {
                    continue;
                }

                // get roles
                ApplicationUser? aUser = await _userManager.FindByIdAsync(rawUser.Id);
                if (aUser != null)
                {
                    IList<string> aUserRoles = await _userManager.GetRolesAsync(aUser);
                    user.Roles = aUserRoles.ToList();

                    user.Roles.Sort();
                }

                // only add a user to the list if they contain roles other than "Member" or "Verified Email"
                if (user.Roles.Except(new string[] { "Member", "Verified Email" }).Any())
                {
                    users.Add(user);
                }
            }

            // sort all users by email
            users = users.OrderBy(u => u.EmailAddress).ToList();

            return Ok(users);
        }

        [HttpGet]
        [Route("Users/{Email}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUser(string Email)
        {
            ApplicationUser? rawUser = await _userManager.FindByEmailAsync(Email);

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

        [HttpPost]
        [Route("Users/{Email}/Roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetUserRoles(string Email, [FromBody] string[] roleList)
        {
            ApplicationUser? user = await _userManager.FindByEmailAsync(Email);

            if (user != null)
            {
                // get roles
                List<string> userRoles = (await _userManager.GetRolesAsync(user)).ToList();

                // delete all roles
                foreach (string role in userRoles)
                {
                    if (role != "Member" && role != "Verified Email")
                    {
                        await _userManager.RemoveFromRoleAsync(user, role);
                    }
                }

                // add requested roles (dependencies are handled automatically)
                foreach (string roleName in roleList)
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                }

                return Ok();
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet]
        [Route("Roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => new { r.Id, r.Name, r.AllowManualAssignment, r.RoleDependsOn }).OrderBy(r => r.Name).ToList();
            return Ok(roles);
        }
    }
}