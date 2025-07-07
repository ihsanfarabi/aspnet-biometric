using Fido2NetLib;
using Fido2NetLib.Objects;
using System.Threading.Tasks;
using aspnet_biometric.Models;
using static Fido2NetLib.Fido2;

namespace aspnet_biometric.Services
{
    public interface IWebAuthnService
    {
        Task<CredentialCreateOptions> GetCredentialCreationOptionsAsync(string username);
        Task<CredentialMakeResult> CompleteRegistrationAsync(AuthenticatorAttestationRawResponse attestationResponse, CredentialCreateOptions storedOptions, string username);
        Task<AssertionOptions> GetAssertionOptionsAsync();
        Task<(AssertionVerificationResult result, User? user)> CompleteAssertionAsync(AuthenticatorAssertionRawResponse clientResponse, AssertionOptions options);
    }
}