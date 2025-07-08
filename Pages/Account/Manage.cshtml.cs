using aspnet_biometric.Models;
using aspnet_biometric.Services;
using aspnet_biometric.Data;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace aspnet_biometric.Pages.Account
{
    [Authorize]
    public class ManageModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IWebAuthnService _webAuthnService;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;

        public ManageModel(UserManager<User> userManager, IWebAuthnService webAuthnService, ApplicationDbContext context, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _webAuthnService = webAuthnService;
            _context = context;
            _signInManager = signInManager;
        }

        public string Username { get; set; } = string.Empty;
        public bool IsBiometricRegistered { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.UserName == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            Username = user.UserName;
            
            // Check if user has biometric credentials registered AND enabled
            var hasCredentials = await _context.Fido2Credentials
                .AnyAsync(c => c.UserId == user.Id);
            
            IsBiometricRegistered = hasCredentials && user.IsBiometricEnabled;

            return Page();
        }

        public async Task<IActionResult> OnPostVerifyPasswordAndStartRegistrationAsync(string password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new JsonResult(new { status = "error", errorMessage = "User not found." });
            }

            // Verify the password
            var passwordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
            {
                return new JsonResult(new { status = "error", errorMessage = "Invalid password. Please try again." });
            }

            // Password is valid, start the biometric registration process
            try
            {
                var options = await _webAuthnService.GetCredentialCreationOptionsAsync(user.UserName!);
                HttpContext.Session.SetString("fido2.attestationOptions", options.ToJson());
                return new JsonResult(new { status = "ok", options = options });
            }
            catch (System.Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }

        public async Task<IActionResult> OnPostToggleBiometricAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new JsonResult(new { status = "error", errorMessage = "User not found." });
            }

            var hasCredentials = await _context.Fido2Credentials
                .AnyAsync(c => c.UserId == user.Id);

            if (hasCredentials)
            {
                // Toggle the biometric enabled flag - no need for new registration
                user.IsBiometricEnabled = !user.IsBiometricEnabled;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    var statusMessage = user.IsBiometricEnabled 
                        ? "Biometric authentication re-enabled using existing credentials." 
                        : "Biometric authentication disabled.";
                    
                    return new JsonResult(new { 
                        status = "ok", 
                        enabled = user.IsBiometricEnabled, 
                        message = statusMessage 
                    });
                }
                else
                {
                    return new JsonResult(new { status = "error", errorMessage = "Failed to update biometric settings." });
                }
            }
            else
            {
                // No credentials registered - user needs to register first
                if (user.IsBiometricEnabled)
                {
                    // User is trying to disable but has no credentials (shouldn't happen)
                    user.IsBiometricEnabled = false;
                    await _userManager.UpdateAsync(user);
                    return new JsonResult(new { status = "ok", enabled = false, message = "Biometric authentication disabled." });
                }
                else
                {
                    // User is trying to enable but has no credentials - show registration flow
                    return new JsonResult(new { status = "needsRegistration", message = "Please register biometric credentials first." });
                }
            }
        }

        public async Task<IActionResult> OnPostMakeCredentialAsync(string username)
        {
            try
            {
                var options = await _webAuthnService.GetCredentialCreationOptionsAsync(username);
                HttpContext.Session.SetString("fido2.attestationOptions", options.ToJson());
                return new JsonResult(options);
            }
            catch (System.Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }

        public async Task<IActionResult> OnPostCompleteRegistrationAsync([FromBody] AuthenticatorAttestationRawResponse attestationResponse)
        {
            try
            {
                var optionsJson = HttpContext.Session.GetString("fido2.attestationOptions");
                var options = CredentialCreateOptions.FromJson(optionsJson);
                var user = await _userManager.GetUserAsync(User);
                
                if (user == null || user.UserName == null)
                {
                    return new JsonResult(new { status = "error", errorMessage = "User not found." });
                }

                var result = await _webAuthnService.CompleteRegistrationAsync(attestationResponse, options, user.UserName);

                // If registration is successful, ensure biometric is enabled
                if (result.Status == "ok")
                {
                    user.IsBiometricEnabled = true;
                    await _userManager.UpdateAsync(user);
                }

                return new JsonResult(result);
            }
            catch (System.Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }
    }
}