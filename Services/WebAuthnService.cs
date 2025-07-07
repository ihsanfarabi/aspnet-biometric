using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using aspnet_biometric.Data;
using aspnet_biometric.Models;
using static Fido2NetLib.Fido2;

namespace aspnet_biometric.Services
{
    public class WebAuthnService : IWebAuthnService
    {
        private readonly IFido2 _fido2;
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<User> _userManager;

        public WebAuthnService(IFido2 fido2, ApplicationDbContext dbContext, UserManager<User> userManager)
        {
            _fido2 = fido2;
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task<CredentialCreateOptions> GetCredentialCreationOptionsAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null)
            {
                // This should not happen for a logged-in user.
                throw new InvalidOperationException($"User '{username}' not found.");
            }

            // Ensure Fido2Id is set for the user if this is their first passkey.
            if (user.Fido2Id == null)
            {
                user.Fido2Id = Encoding.UTF8.GetBytes(user.UserName ?? username);
                await _dbContext.SaveChangesAsync();
            }

            var existingCredentials = new List<PublicKeyCredentialDescriptor>();
            existingCredentials = await _dbContext.Fido2Credentials
                .Where(c => c.UserId == user.Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.UserHandle))
                .ToListAsync();

            var authenticatorSelection = new AuthenticatorSelection
            {
                RequireResidentKey = true,
                UserVerification = UserVerificationRequirement.Preferred
            };

            var options = _fido2.RequestNewCredential(
                new Fido2User { Id = user.Fido2Id, Name = user.UserName, DisplayName = user.DisplayName },
                existingCredentials,
                authenticatorSelection,
                AttestationConveyancePreference.None
            );

            return options;
        }

        public async Task<CredentialMakeResult> CompleteRegistrationAsync(AuthenticatorAttestationRawResponse attestationResponse, CredentialCreateOptions storedOptions, string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            var user = await _dbContext.Users.FirstAsync(u => u.UserName == username);

            var result = await _fido2.MakeNewCredentialAsync(attestationResponse, storedOptions, async (args, cancellationToken) =>
            {
                var creds = await _dbContext.Fido2Credentials.FirstOrDefaultAsync(c => c.UserHandle.SequenceEqual(args.CredentialId), cancellationToken);
                return creds == null;
            });

            if (result.Status == "ok" && result.Result != null)
            {
                var credential = new Fido2Credential
                {
                    UserId = user.Id,
                    PublicKey = result.Result.PublicKey,
                    UserHandle = attestationResponse.Id,
                    SignatureCounter = result.Result.Counter,
                    CredType = result.Result.CredType,
                    RegDate = DateTime.Now,
                    AaGuid = result.Result.Aaguid
                };
                _dbContext.Fido2Credentials.Add(credential);
                await _dbContext.SaveChangesAsync();
            }

            return result;
        }

        public async Task<AssertionOptions> GetAssertionOptionsAsync()
        {
            var options = _fido2.GetAssertionOptions(
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required
            );

            return options;
        }

        public async Task<(AssertionVerificationResult result, User? user)> CompleteAssertionAsync(AuthenticatorAssertionRawResponse assertionResponse, AssertionOptions storedOptions)
        {
            var creds = await _dbContext.Fido2Credentials
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserHandle != null && c.UserHandle.SequenceEqual(assertionResponse.Id));

            if (creds == null || creds.UserId == null || creds.User == null)
            {
                throw new InvalidOperationException("Credential not found");
            }

            var user = creds.User;

            if (user == null || creds.PublicKey == null)
            {
                throw new InvalidOperationException("User or public key not found");
            }

            var result = await _fido2.MakeAssertionAsync(assertionResponse, storedOptions, creds.PublicKey, creds.SignatureCounter, (args, cancellationToken) =>
            {
                if (user.Fido2Id == null || args.UserHandle == null) return Task.FromResult(false);
                return Task.FromResult(user.Fido2Id.SequenceEqual(args.UserHandle));
            });

            return (result, user);
        }
    }
}