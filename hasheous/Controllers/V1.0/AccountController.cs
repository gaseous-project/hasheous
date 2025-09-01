using System.Data;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Web;
using Authentication;
using Classes;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Antiforgery;
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
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<AccountController>();
        }

        /// <summary>
        /// Manages the "Verified Email" role based on the user's email confirmation status
        /// </summary>
        /// <param name="user">The user to update</param>
        private async Task UpdateVerifiedEmailRole(ApplicationUser user)
        {
            const string verifiedEmailRole = "Verified Email";
            bool isEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            bool hasVerifiedEmailRole = await _userManager.IsInRoleAsync(user, verifiedEmailRole);

            if (isEmailConfirmed && !hasVerifiedEmailRole)
            {
                // Email is confirmed but user doesn't have the verified email role - add it
                await _userManager.AddToRoleAsync(user, verifiedEmailRole);
            }
            else if (!isEmailConfirmed && hasVerifiedEmailRole)
            {
                // Email is not confirmed but user has the verified email role - remove it
                await _userManager.RemoveFromRoleAsync(user, verifiedEmailRole);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("Login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    Logging.Log(Logging.LogType.Information, "Login", model.Email + " has logged in, from IP: " + HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Ok(result.ToString());
                }
                // if (result.RequiresTwoFactor)
                // {
                //     return RedirectToAction(nameof(SendCode), new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                // }
                if (result.IsLockedOut)
                {
                    Logging.Log(Logging.LogType.Warning, "Login", model.Email + " was unable to login due to a locked account. Login attempt from IP: " + HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Unauthorized();
                }
                else
                {
                    Logging.Log(Logging.LogType.Critical, "Login", "An unknown error occurred during login by " + model.Email + ". Login attempt from IP: " + HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Unauthorized();
                }
            }

            // If we got this far, something failed, redisplay form
            Logging.Log(Logging.LogType.Critical, "Login", "An unknown error occurred during login by " + model.Email + ". Login attempt from IP: " + HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized();
        }

        [HttpPost]
        [Route("LogOff")]
        public async Task<IActionResult> LogOff()
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            await _signInManager.SignOutAsync();
            if (userName != null)
            {
                Logging.Log(Logging.LogType.Information, "Login", userName + " has logged out");
            }
            return Ok();
        }

        [HttpGet]
        [Route("Profile/Basic")]
        [Authorize]
        public async Task<IActionResult> ProfileBasic()
        {
            ProfileBasicViewModel profile = new ProfileBasicViewModel();
            profile.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ApplicationUser user = await _userManager.FindByIdAsync(profile.UserId);
            profile.UserName = _userManager.GetUserName(HttpContext.User);
            profile.EmailAddress = await _userManager.GetEmailAsync(user);
            profile.EmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            profile.Roles = new List<string>(await _userManager.GetRolesAsync(user));
            profile.SecurityProfile = user.SecurityProfile;
            profile.Roles.Sort();

            return Ok(profile);
        }

        [HttpGet]
        [Route("Profile/Basic/profile.js")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        public async Task<IActionResult> ProfileBasicFile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ProfileBasicViewModel profile = new ProfileBasicViewModel();
                profile.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                profile.UserName = _userManager.GetUserName(HttpContext.User);
                profile.EmailAddress = await _userManager.GetEmailAsync(user);
                profile.EmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
                profile.Roles = new List<string>(await _userManager.GetRolesAsync(user));
                profile.SecurityProfile = user.SecurityProfile;
                profile.Roles.Sort();

                string profileString = "var userProfile = " + Newtonsoft.Json.JsonConvert.SerializeObject(profile, Newtonsoft.Json.Formatting.Indented) + ";";

                byte[] bytes = Encoding.UTF8.GetBytes(profileString);
                return File(bytes, "text/javascript");
            }
            else
            {
                string profileString = "var userProfile = null;";

                byte[] bytes = Encoding.UTF8.GetBytes(profileString);
                return File(bytes, "text/javascript");
            }
        }

        [HttpPost]
        [Route("ChangePassword")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login");
                }

                // ChangePasswordAsync changes the user password
                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

                // The new password did not meet the complexity rules or
                // the current password is incorrect. Add these errors to
                // the ModelState and rerender ChangePassword view
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Unauthorized(result);
                }

                // Upon successfully changing the password refresh sign-in cookie
                await _signInManager.RefreshSignInAsync(user);
                return Ok();
            }

            return NotFound();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [Route("Register")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            // ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                ApplicationUser user = new ApplicationUser
                {
                    UserName = model.UserName,
                    NormalizedUserName = model.UserName.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper()
                };
                // check for a duplicate email address
                if (_userManager.Options.User.RequireUniqueEmail == true)
                {
                    var existingUser = await _userManager.FindByEmailAsync(user.NormalizedEmail);
                    if (existingUser != null)
                    {
                        IdentityError identityError = new IdentityError { Code = "NotUniqueEmail", Description = user.UserName + " is already taken" };
                        IdentityResult identityResult = IdentityResult.Failed(identityError);
                        Logging.Log(Logging.LogType.Information, "User Management", "Unable to create new user " + model.Email + ". Account already exists.");
                        return Ok(identityResult);
                    }
                }
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=532713
                    // Send an email with this link
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                    try
                    {
                        await _emailSender.SendEmailAsync(model.Email, "Confirm your account",
                           "Please confirm your account by clicking this link: <a href=\"" + callbackUrl + "\">link</a>");
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "Email", "Error sending confirmation email to " + model.Email + ": " + ex.Message);
                        // If email sending fails, we can still create the user but they won't be able to confirm their email
                        // You might want to handle this differently in production
                    }

                    // add all users to the member role
                    await _userManager.AddToRoleAsync(user, "Member");

                    // Update the "Verified Email" role based on email confirmation status
                    await UpdateVerifiedEmailRole(user);

                    //await _signInManager.SignInAsync(user, isPersistent: false);

                    Logging.Log(Logging.LogType.Information, "User Management", "New user " + model.Email + " created with password.");

                    if (returnUrl != null)
                    {
                        return Ok(returnUrl);
                    }
                }
                return Ok(result);
            }
            else
            {
                return NotFound();
            }
        }

        // GET: /Account/ConfirmEmail
        [HttpGet]
        [AllowAnonymous]
        [Route("ConfirmEmail")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return Unauthorized("Error");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized("Error");
            }
            var result = await _userManager.ConfirmEmailAsync(user, code);
            if (result.Succeeded == true)
            {
                // Update the "Verified Email" role
                await UpdateVerifiedEmailRole(user);
                return new RedirectResult("/index.html?page=emailconfirmed");
            }
            else
            {
                return Ok(result);
            }
        }

        /// <summary>
        /// Resend email verification to the current user
        /// </summary>
        /// <returns>Success or error result</returns>
        [HttpPost]
        [Route("ResendEmailVerification")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize]
        public async Task<IActionResult> ResendEmailVerification()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                return BadRequest("Email is already confirmed");
            }

            try
            {
                // Generate email confirmation token
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                
                // Send confirmation email
                await _emailSender.SendEmailAsync(user.Email, "Confirm your account",
                   "Please confirm your account by clicking this link: <a href=\"" + callbackUrl + "\">link</a>");

                return Ok("Verification email sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error sending verification email to {Email}: {Message}", user.Email, ex.Message);
                return StatusCode(500, "Error sending verification email. Please try again later.");
            }
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [Route("ForgotPassword")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return Ok();
                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=532713
                // Send an email with this link
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                //var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                var callbackUrl = new Uri(string.Format("{0}://{1}{2}&userId={3}&code={4}", [Request.Scheme, Request.Host, "/index.html?page=resetpassword", user.Id, HttpUtility.UrlEncode(code)]));
                await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                  "Please reset your password by clicking here: <a href=\"" + callbackUrl + "\">link</a>");
                return Ok();
            }

            // If we got this far, something failed, redisplay form
            return Ok();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [Route("ResetPassword")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Ok();
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return Ok();
            }
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return Ok(result);
            }
            return Ok();
        }

        //
        // POST: /Account/Delete
        [HttpPost]
        [Route("Delete")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Delete()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return BadRequest();
            }
            else
            {
                await _signInManager.SignOutAsync();
                var result = await _userManager.DeleteAsync(user);
                return Ok();
            }
        }

        [HttpGet]
        [Route("APIKey")]
        public async Task<IActionResult> GetAPIKey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return BadRequest();
            }
            else
            {
                Authentication.ApiKey apiKey = new ApiKey();
                string? userKey = apiKey.GetApiKey(user.Id);
                if (userKey != null)
                {
                    return Ok(userKey);
                }
                else
                {
                    userKey = apiKey.SetApiKey(user.Id);
                    return Ok(userKey);
                }
            }
        }

        [HttpPost]
        [Route("APIKey")]
        public async Task<IActionResult> SetAPIKey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return BadRequest();
            }
            else
            {
                Authentication.ApiKey apiKey = new ApiKey();
                string? userKey = apiKey.SetApiKey(user.Id);
                return Ok(userKey);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("antiforgery-token")]
        public IActionResult GetAntiforgeryToken([FromServices] IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { token = tokens.RequestToken });
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("social-login")]
        public IActionResult SocialLoginAvailable()
        {
            // This endpoint is used to check if social login is available
            List<string> availableLogins = new List<string>();

            // Check if Google login is configured
            if (Config.SocialAuthConfiguration.GoogleAuthEnabled)
            {
                availableLogins.Add("Google");
            }
            if (Config.SocialAuthConfiguration.MicrosoftAuthEnabled)
            {
                availableLogins.Add("Microsoft");
            }

            return Ok(availableLogins);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("signin-google")]
        public IActionResult SignInGoogle(string returnUrl = "/")
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account", new { ReturnUrl = returnUrl }, protocol: "https");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("signin-microsoft")]
        public IActionResult SignInMicrosoft(string returnUrl = "/")
        {
            var redirectUrl = Url.Action("MicrosoftResponse", "Account", new { ReturnUrl = returnUrl }, protocol: "https");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Microsoft", redirectUrl);
            return Challenge(properties, "Microsoft");
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("GoogleResponse")]
        public async Task<IActionResult> GoogleResponse(string returnUrl = "/")
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // Get the email from the external provider
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (email == null)
                {
                    return Unauthorized();
                }

                // Try to find an existing user with this email
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    // Link the Google login to the existing user
                    var addLoginResult = await _userManager.AddLoginAsync(user, info);
                    if (addLoginResult.Succeeded)
                    {
                        // Update the "Verified Email" role based on email confirmation status
                        await UpdateVerifiedEmailRole(user);
                        await _signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(returnUrl);
                    }
                    else
                    {
                        return Unauthorized(addLoginResult.Errors);
                    }
                }
                else
                {
                    // No user exists, create a new one
                    user = new ApplicationUser { UserName = email, Email = email };
                    var identityResult = await _userManager.CreateAsync(user);
                    if (identityResult.Succeeded)
                    {
                        await _userManager.AddLoginAsync(user, info);
                        // var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                        // await _emailSender.SendEmailAsync(user.Email, "Confirm your account",
                        //    "Please confirm your account by clicking this link: <a href=\"" + callbackUrl + "\">link</a>");

                        // mark email address as confirmed
                        user.EmailConfirmed = true;
                        var updateResult = await _userManager.UpdateAsync(user);
                        if (!updateResult.Succeeded)
                        {
                            return Unauthorized(updateResult.Errors);
                        }

                        // add all users to the member role
                        await _userManager.AddToRoleAsync(user, "Member");

                        // Update the "Verified Email" role based on email confirmation status
                        await UpdateVerifiedEmailRole(user);

                        await _signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(returnUrl);
                    }
                    return Unauthorized(identityResult.Errors);
                }
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("MicrosoftResponse")]
        public async Task<IActionResult> MicrosoftResponse(string returnUrl = "/")
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // Get the email from the external provider
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (email == null)
                {
                    return Unauthorized();
                }

                // Try to find an existing user with this email
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    // Link the Microsoft login to the existing user
                    var addLoginResult = await _userManager.AddLoginAsync(user, info);
                    if (addLoginResult.Succeeded)
                    {
                        // Update the "Verified Email" role based on email confirmation status
                        await UpdateVerifiedEmailRole(user);
                        await _signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(returnUrl);
                    }
                    else
                    {
                        return Unauthorized(addLoginResult.Errors);
                    }
                }
                else
                {
                    // No user exists, create a new one
                    user = new ApplicationUser { UserName = email, Email = email };
                    var identityResult = await _userManager.CreateAsync(user);
                    if (identityResult.Succeeded)
                    {
                        await _userManager.AddLoginAsync(user, info);

                        // add all users to the member role
                        await _userManager.AddToRoleAsync(user, "Member");

                        // var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                        // await _emailSender.SendEmailAsync(user.Email, "Confirm your account",
                        //    "Please confirm your account by clicking this link: <a href=\"" + callbackUrl + "\">link</a>");

                        // mark email address as confirmed
                        user.EmailConfirmed = true;
                        var updateResult = await _userManager.UpdateAsync(user);
                        if (!updateResult.Succeeded)
                        {
                            return Unauthorized(updateResult.Errors);
                        }

                        // Update the "Verified Email" role based on email confirmation status
                        await UpdateVerifiedEmailRole(user);

                        await _signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(returnUrl);
                    }
                    return Unauthorized(identityResult.Errors);
                }
            }
        }

        [HttpGet]
        [Authorize]
        [Route("link-login/{provider}")]
        public IActionResult LinkLogin(string provider, string returnUrl = "/")
        {
            var redirectUrl = Url.Action("LinkLoginCallback", "Account", new { ReturnUrl = returnUrl }, protocol: "https");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, _userManager.GetUserId(User));
            return Challenge(properties, provider);
        }

        [HttpGet]
        [Authorize]
        [Route("LinkLoginCallback")]
        public async Task<IActionResult> LinkLoginCallback(string returnUrl = "/")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var info = await _signInManager.GetExternalLoginInfoAsync(await _userManager.GetUserIdAsync(user));
            if (info == null)
                return Unauthorized("External login info not found");

            var result = await _userManager.AddLoginAsync(user, info);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return LocalRedirect(returnUrl);
            }
            else
            {
                return Unauthorized(result.Errors);
            }
        }

        [HttpGet]
        [Authorize]
        [Route("unlink-login/{provider}")]
        public async Task<IActionResult> UnlinkLogin(string provider, string returnUrl = "/")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            var login = (await _userManager.GetLoginsAsync(user)).FirstOrDefault(x => x.LoginProvider == provider);
            if (login == null)
                return NotFound("Login not found");

            var result = await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
            if (result.Succeeded)
            {
                // Optionally: sign in the user again
                await _signInManager.RefreshSignInAsync(user);
                return LocalRedirect(returnUrl);
            }
            else
            {
                return Unauthorized(result.Errors);
            }
        }

        [HttpGet]
        [Authorize]
        [Route("linked-logins")]
        public async Task<IActionResult> GetLinkedLogins()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("User not found");

            var logins = await _userManager.GetLoginsAsync(user);
            var linkedLogins = logins.Select(x => new { x.LoginProvider, x.ProviderKey }).ToList();

            return Ok(linkedLogins);
        }
    }
}