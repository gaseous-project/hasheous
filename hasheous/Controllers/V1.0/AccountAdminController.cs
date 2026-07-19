using System.Data;
using System.Security.Claims;
using System.Text;
using Authentication;
using Classes;
using hasheous.Classes;
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
        private const string AdminUsersCachePrefix = "AccountAdminUsers";

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
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            string cacheKey = RedisConnection.GenerateKey(AdminUsersCachePrefix, new { CurrentUserId = currentUserId });

            if (Config.RedisConfiguration.Enabled)
            {
                List<UserViewModel>? cachedUsers = await RedisConnection.GetCacheItem<List<UserViewModel>>(cacheKey);
                if (cachedUsers != null)
                {
                    return Ok(cachedUsers);
                }
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            List<Dictionary<string, object>> rows = await db.ExecuteCMDDictAsync(@"
SELECT
    u.Id,
    u.NormalizedEmail,
    u.Email,
    u.LockoutEnabled,
    u.LockoutEnd,
    u.SecurityProfile,
    GROUP_CONCAT(DISTINCT r.Name ORDER BY r.Name SEPARATOR ',') AS RolesCsv
FROM Users u
INNER JOIN UserRoles ur ON ur.UserId = u.Id
INNER JOIN Roles r ON r.Id = ur.RoleId
WHERE (@currentUserId = '' OR u.Id <> @currentUserId)
GROUP BY u.Id, u.NormalizedEmail, u.Email, u.LockoutEnabled, u.LockoutEnd, u.SecurityProfile
HAVING SUM(CASE WHEN r.Name NOT IN ('Member', 'Verified Email') THEN 1 ELSE 0 END) > 0
ORDER BY u.NormalizedEmail, u.Email;",
            new Dictionary<string, object>
            {
                { "currentUserId", currentUserId }
            });

            List<UserViewModel> users = new List<UserViewModel>(rows.Count);

            foreach (Dictionary<string, object> row in rows)
            {
                string normalizedEmail = row["NormalizedEmail"] == DBNull.Value ? string.Empty : Convert.ToString(row["NormalizedEmail"]) ?? string.Empty;
                string email = row["Email"] == DBNull.Value ? string.Empty : Convert.ToString(row["Email"]) ?? string.Empty;
                string rolesCsv = row["RolesCsv"] == DBNull.Value ? string.Empty : Convert.ToString(row["RolesCsv"]) ?? string.Empty;
                string securityProfileString = row["SecurityProfile"] == DBNull.Value ? string.Empty : Convert.ToString(row["SecurityProfile"]) ?? string.Empty;

                SecurityProfileViewModel securityProfile = new SecurityProfileViewModel();
                if (!string.IsNullOrEmpty(securityProfileString) && securityProfileString != "null")
                {
                    SecurityProfileViewModel? parsedSecurityProfile = Newtonsoft.Json.JsonConvert.DeserializeObject<SecurityProfileViewModel>(securityProfileString);
                    if (parsedSecurityProfile != null)
                    {
                        securityProfile = parsedSecurityProfile;
                    }
                }

                DateTimeOffset? lockoutEnd = null;
                if (row["LockoutEnd"] != DBNull.Value)
                {
                    if (row["LockoutEnd"] is DateTime lockoutDate)
                    {
                        lockoutEnd = new DateTimeOffset(lockoutDate);
                    }
                    else if (DateTimeOffset.TryParse(Convert.ToString(row["LockoutEnd"]), out DateTimeOffset parsedLockoutEnd))
                    {
                        lockoutEnd = parsedLockoutEnd;
                    }
                }

                bool lockoutEnabled = false;
                object? lockoutEnabledValue = row["LockoutEnabled"];
                if (lockoutEnabledValue != DBNull.Value)
                {
                    if (lockoutEnabledValue is bool asBool)
                    {
                        lockoutEnabled = asBool;
                    }
                    else if (bool.TryParse(Convert.ToString(lockoutEnabledValue), out bool parsedBool))
                    {
                        lockoutEnabled = parsedBool;
                    }
                    else
                    {
                        lockoutEnabled = Convert.ToInt32(lockoutEnabledValue) != 0;
                    }
                }

                users.Add(new UserViewModel
                {
                    Id = Convert.ToString(row["Id"]) ?? string.Empty,
                    EmailAddress = (!string.IsNullOrEmpty(normalizedEmail) ? normalizedEmail : email).ToLowerInvariant(),
                    LockoutEnabled = lockoutEnabled,
                    LockoutEnd = lockoutEnd,
                    SecurityProfile = securityProfile,
                    Roles = rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                });
            }

            if (Config.RedisConfiguration.Enabled)
            {
                await RedisConnection.SetCacheItem(cacheKey, users, TimeSpan.FromMinutes(5));
            }

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

                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.PurgeCache(AdminUsersCachePrefix);
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