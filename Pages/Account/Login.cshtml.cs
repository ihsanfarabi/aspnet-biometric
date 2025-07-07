using System.ComponentModel.DataAnnotations;
using aspnet_biometric.Models;
using aspnet_biometric.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace aspnet_biometric.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IWebAuthnService _webAuthnService;

        public LoginModel(SignInManager<User> signInManager, ILogger<LoginModel> logger, IWebAuthnService webAuthnService)
        {
            _signInManager = signInManager;
            _logger = logger;
            _webAuthnService = webAuthnService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        public async Task<IActionResult> OnPostGetAssertionOptionsUsernamelessAsync()
        {
            try
            {
                var options = await _webAuthnService.GetAssertionOptionsAsync();
                HttpContext.Session.SetString("fido2.assertionOptions", options.ToJson());
                return new JsonResult(options);
            }
            catch (Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }

        public async Task<IActionResult> OnPostMakeAssertionAsync([FromBody] AuthenticatorAssertionRawResponse clientResponse)
        {
            try
            {
                var optionsJson = HttpContext.Session.GetString("fido2.assertionOptions");
                if (string.IsNullOrEmpty(optionsJson))
                {
                    return new JsonResult(new { status = "error", errorMessage = "Assertion options not found in session." });
                }
                var options = AssertionOptions.FromJson(optionsJson);

                var (result, user) = await _webAuthnService.CompleteAssertionAsync(clientResponse, options);

                if (result.Status == "ok" && user != null)
                {
                    await _signInManager.SignInAsync(user, isPersistent: true);
                }

                return new JsonResult(result);
            }
            catch (Exception e)
            {
                return new JsonResult(new { status = "error", errorMessage = e.Message });
            }
        }
    }
}