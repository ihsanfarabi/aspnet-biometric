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
            
            // Check if user has any biometric credentials registered
            IsBiometricRegistered = await _context.Fido2Credentials
                .AnyAsync(c => c.UserId == user.Id);

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
                // Remove all biometric credentials for this user
                var credentials = await _context.Fido2Credentials
                    .Where(c => c.UserId == user.Id)
                    .ToListAsync();
                
                _context.Fido2Credentials.RemoveRange(credentials);
                await _context.SaveChangesAsync();

                return new JsonResult(new { status = "ok", enabled = false, message = "Biometric authentication disabled." });
            }
            else
            {
                // This should not happen with the new flow - enabling should go through password verification
                return new JsonResult(new { status = "error", errorMessage = "Please use the proper registration flow." });
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

                return new JsonResult(result);
            }
            catch (System.Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }
    }
}