using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using aspnet_biometric.Models;

namespace aspnet_biometric.Pages
{
    public class SelectAuthenticatorModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<SelectAuthenticatorModel> _logger;

        public SelectAuthenticatorModel(
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            ILogger<SelectAuthenticatorModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        public string? ReturnUrl { get; set; }
        public string? UserEmail { get; set; }
        public string? UserDisplayName { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            // Check if there's a pre-authenticated user in session
            var tempUserId = HttpContext.Session.GetString("temp_user_id");
            if (string.IsNullOrEmpty(tempUserId))
            {
                // No pre-authenticated user, redirect to login
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(tempUserId);
            if (user == null)
            {
                // Invalid user, clear session and redirect to login
                HttpContext.Session.Remove("temp_user_id");
                return RedirectToPage("/Index");
            }

            UserEmail = user.Email;
            UserDisplayName = user.DisplayName ?? user.Email;
            ReturnUrl = returnUrl ?? Url.Content("~/Home");

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostEmailAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/Home");

            var tempUserId = HttpContext.Session.GetString("temp_user_id");
            if (string.IsNullOrEmpty(tempUserId))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(tempUserId);
            if (user == null)
            {
                HttpContext.Session.Remove("temp_user_id");
                return RedirectToPage("/Index");
            }

            try
            {
                // Sign in the user with email authentication method
                await _signInManager.SignInAsync(user, isPersistent: true);
                
                // Clear the temporary session
                HttpContext.Session.Remove("temp_user_id");
                
                _logger.LogInformation("User authenticated via email method.");
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email authentication.");
                ErrorMessage = "An error occurred during authentication. Please try again.";
                return RedirectToPage(new { returnUrl });
            }
        }

        public async Task<IActionResult> OnPostOtpAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/Home");

            var tempUserId = HttpContext.Session.GetString("temp_user_id");
            if (string.IsNullOrEmpty(tempUserId))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(tempUserId);
            if (user == null)
            {
                HttpContext.Session.Remove("temp_user_id");
                return RedirectToPage("/Index");
            }

            try
            {
                // Sign in the user with OTP authentication method
                await _signInManager.SignInAsync(user, isPersistent: true);
                
                // Clear the temporary session
                HttpContext.Session.Remove("temp_user_id");
                
                _logger.LogInformation("User authenticated via OTP method.");
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP authentication.");
                ErrorMessage = "An error occurred during authentication. Please try again.";
                return RedirectToPage(new { returnUrl });
            }
        }
    }
} 