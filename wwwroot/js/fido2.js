// FIDO2 client-side script

function arrayBufferToBase64Url(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer)))
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=/g, '');
}

// Handle biometric toggle switch
if (document.getElementById('biometricToggle')) {
    console.log('Biometric toggle found, setting up event listener');
    document.getElementById('biometricToggle').addEventListener('change', async (e) => {
        const toggle = e.target;
        const isEnabled = toggle.checked;
        
        console.log('Toggle changed, new state:', isEnabled);
        
        // Check if anti-forgery token exists
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!tokenElement) {
            console.error('Anti-forgery token not found!');
            toggle.checked = !isEnabled; // Revert toggle
            alert('Anti-forgery token not found. Please refresh the page.');
            return;
        }
        
        const token = tokenElement.value;
        console.log('Anti-forgery token found:', token ? 'Yes' : 'No');
        
        if (isEnabled) {
            // User wants to enable biometric - check if they have existing credentials
            await enableBiometric(toggle, token);
        } else {
            // User wants to disable biometric - proceed directly
            await disableBiometric(toggle, token);
        }
    });
} else {
    console.log('Biometric toggle element not found!');
}

function showTermsAndConditions(toggle, token) {
    console.log('Showing terms and conditions modal...');
    
    // Show the terms modal
    const termsModal = new bootstrap.Modal(document.getElementById('termsModal'));
    termsModal.show();
    
    // Handle accept terms
    document.getElementById('acceptTerms').onclick = function() {
        console.log('Terms accepted, showing password confirmation...');
        termsModal.hide();
        showPasswordConfirmation(toggle, token);
    };
    
    // Handle decline terms
    document.getElementById('declineTerms').onclick = function() {
        console.log('Terms declined, reverting toggle...');
        toggle.checked = false;
        termsModal.hide();
    };
    
    // Handle modal close (X button or backdrop)
    document.getElementById('termsModal').addEventListener('hidden.bs.modal', function() {
        if (!document.getElementById('passwordModal').classList.contains('show')) {
            // Only revert if password modal is not showing (user closed terms modal)
            toggle.checked = false;
        }
    }, { once: true });
}

function showPasswordConfirmation(toggle, token) {
    console.log('Showing password confirmation modal...');
    
    // Clear previous password and error
    document.getElementById('confirmPassword').value = '';
    document.getElementById('passwordError').style.display = 'none';
    
    // Show the password modal
    const passwordModal = new bootstrap.Modal(document.getElementById('passwordModal'));
    passwordModal.show();
    
    // Handle password confirmation
    document.getElementById('confirmPasswordBtn').onclick = async function() {
        const password = document.getElementById('confirmPassword').value;
        
        if (!password) {
            showPasswordError('Please enter your password.');
            return;
        }
        
        console.log('Password entered, verifying...');
        await verifyPasswordAndRegister(toggle, token, password, passwordModal);
    };
    
    // Handle Enter key in password field
    document.getElementById('confirmPassword').onkeypress = function(e) {
        if (e.key === 'Enter') {
            document.getElementById('confirmPasswordBtn').click();
        }
    };
    
    // Handle modal close
    document.getElementById('passwordModal').addEventListener('hidden.bs.modal', function() {
        toggle.checked = false; // Revert toggle if modal is closed without confirmation
    }, { once: true });
}

function showPasswordError(message) {
    const errorDiv = document.getElementById('passwordError');
    errorDiv.textContent = message;
    errorDiv.style.display = 'block';
}

async function verifyPasswordAndRegister(toggle, token, password, passwordModal) {
    try {
        console.log('Verifying password and starting biometric registration...');
        
        // Create form data with password and token
        const formData = new FormData();
        formData.append('__RequestVerificationToken', token);
        formData.append('password', password);
        
        const response = await fetch('/Account/Manage?handler=VerifyPasswordAndStartRegistration', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        console.log('Password verification response status:', response.status);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Password verification response:', result);

        if (result.status === 'ok') {
            // Password verified, close modal and start biometric registration
            passwordModal.hide();
            console.log('Password verified, starting biometric registration...');
            await registerBiometricCredential(result.options);
        } else {
            // Password verification failed
            showPasswordError(result.errorMessage || 'Invalid password. Please try again.');
        }
    } catch (err) {
        console.error('Password verification error:', err);
        showPasswordError('An error occurred. Please try again.');
    }
}

async function enableBiometric(toggle, token) {
    try {
        console.log('Attempting to enable biometric authentication...');
        
        // Create form data with the anti-forgery token
        const formData = new FormData();
        formData.append('__RequestVerificationToken', token);
        
        const response = await fetch('/Account/Manage?handler=ToggleBiometric', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        console.log('Enable biometric response status:', response.status);
        
        if (!response.ok) {
            if (response.status === 302) {
                throw new Error('Authentication failed. Please refresh the page and try again.');
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Enable biometric response:', result);

        if (result.status === 'ok') {
            // Successfully enabled using existing credentials
            console.log('Successfully re-enabled biometric authentication with existing credentials');
            updateBiometricStatus(result.enabled);
            alert(result.message);
        } else if (result.status === 'needsRegistration') {
            // User needs to register new credentials - show terms and conditions
            console.log('User needs to register new credentials');
            showTermsAndConditions(toggle, token);
        } else {
            // Error occurred
            console.error('Server returned error:', result.errorMessage);
            toggle.checked = false; // Revert toggle
            alert('Error: ' + result.errorMessage);
        }
    } catch (err) {
        console.error('Enable biometric error:', err);
        toggle.checked = false; // Revert toggle
        alert('An error occurred: ' + err.message);
    }
}

async function disableBiometric(toggle, token) {
    try {
        console.log('Disabling biometric authentication...');
        
        // Create form data with the anti-forgery token
        const formData = new FormData();
        formData.append('__RequestVerificationToken', token);
        
        const response = await fetch('/Account/Manage?handler=ToggleBiometric', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        console.log('Disable biometric response status:', response.status);
        
        if (!response.ok) {
            if (response.status === 302) {
                throw new Error('Authentication failed. Please refresh the page and try again.');
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Disable biometric response:', result);

        if (result.status === 'ok') {
            // Successfully disabled
            console.log('Successfully disabled biometric authentication');
            updateBiometricStatus(result.enabled);
            alert(result.message);
        } else {
            // Error occurred
            console.error('Server returned error:', result.errorMessage);
            toggle.checked = true; // Revert toggle
            alert('Error: ' + result.errorMessage);
        }
    } catch (err) {
        console.error('Disable biometric error:', err);
        toggle.checked = true; // Revert toggle
        alert('An error occurred: ' + err.message);
    }
}

async function registerBiometricCredential(options) {
    try {
        console.log('Starting biometric registration with options:', options);

        // Validate that we have the required properties
        if (!options.challenge) {
            throw new Error('Server response missing challenge property');
        }
        if (!options.user || !options.user.id) {
            throw new Error('Server response missing user.id property');
        }

        options.challenge = Uint8Array.from(atob(options.challenge.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
        options.user.id = Uint8Array.from(atob(options.user.id.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));

        const credential = await navigator.credentials.create({
            publicKey: options
        });
        console.log('Credential created:', credential);

        const attestationResponse = {
            id: arrayBufferToBase64Url(credential.rawId),
            rawId: arrayBufferToBase64Url(credential.rawId),
            response: {
                attestationObject: arrayBufferToBase64Url(credential.response.attestationObject),
                clientDataJSON: arrayBufferToBase64Url(credential.response.clientDataJSON),
            },
            type: credential.type
        };
        console.log('Sending attestation response to server:', attestationResponse);

        const completeResponse = await fetch('/Account/Manage?handler=CompleteRegistration', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(attestationResponse)
        });

        const completeResult = await completeResponse.json();
        console.log('Server registration response:', completeResult);

        if (completeResult.status === 'ok') {
            updateBiometricStatus(true);
            alert('Biometric authentication enabled successfully!');
        } else {
            document.getElementById('biometricToggle').checked = false; // Revert toggle
            alert('Registration failed: ' + completeResult.errorMessage);
        }
    } catch (err) {
        console.error('Biometric registration error:', err);
        document.getElementById('biometricToggle').checked = false; // Revert toggle
        alert('An error occurred during registration: ' + err.message);
    }
}

function updateBiometricStatus(enabled) {
    const statusText = document.querySelector('.card-text span');
    if (statusText) {
        if (enabled) {
            statusText.className = 'text-success';
            statusText.textContent = 'Enabled - Secure passwordless login';
        } else {
            statusText.className = 'text-warning';
            statusText.textContent = 'Disabled - Credentials preserved';
        }
    }
}

if (document.getElementById('register-form')) {
    document.getElementById('register-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const username = document.getElementById('username').value;

            console.log('Registration started for user:', username);

            const response = await fetch(`/Account/Manage?handler=MakeCredential&username=${username}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                }
            });

            const options = await response.json();
            console.log('Credential creation options received:', options);

            // Check if the response has an error
            if (options.status === 'error') {
                throw new Error(options.errorMessage || 'Server returned an error');
            }

            await registerBiometricCredential(options);
        } catch (err) {
            console.error('Registration error:', err);
            alert('An error occurred during registration. Details: ' + err.message);
        }
    });
}

if (document.getElementById('login-usernameless')) {
    document.getElementById('login-usernameless').addEventListener('click', async (e) => {
        e.preventDefault();
        try {
            console.log('Usernameless login started');

            // Determine which page we're on to call the correct endpoint
            const isIndexPage = window.location.pathname === '/' || window.location.pathname === '/Index';
            const baseUrl = isIndexPage ? '/' : '/Account/Login';

            const response = await fetch(`${baseUrl}?handler=GetAssertionOptionsUsernameless`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                }
            });

            const options = await response.json();
            console.log('Assertion options received:', options);

            // Check if the response has an error
            if (options.status === 'error') {
                throw new Error(options.errorMessage || 'Server returned an error');
            }

            // Validate that we have the required properties
            if (!options.challenge) {
                throw new Error('Server response missing challenge property');
            }

            options.challenge = Uint8Array.from(atob(options.challenge.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
            if (options.allowCredentials) {
                options.allowCredentials.forEach(cred => {
                    cred.id = Uint8Array.from(atob(cred.id.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
                });
            }
            
            const credential = await navigator.credentials.get({
                publicKey: options
            });
            console.log('Assertion credential received:', credential);

            const assertionResponse = {
                id: arrayBufferToBase64Url(credential.rawId),
                rawId: arrayBufferToBase64Url(credential.rawId),
                response: {
                    authenticatorData: arrayBufferToBase64Url(credential.response.authenticatorData),
                    clientDataJSON: arrayBufferToBase64Url(credential.response.clientDataJSON),
                    signature: arrayBufferToBase64Url(credential.response.signature),
                    userHandle: arrayBufferToBase64Url(credential.response.userHandle),
                },
                type: credential.type
            };
            console.log('Sending assertion response to server:', assertionResponse);

            const completeResponse = await fetch(`${baseUrl}?handler=MakeAssertion`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify(assertionResponse)
            });

            const completeResult = await completeResponse.json();
            console.log('Server login response:', completeResult);

            if (completeResult.status === 'ok') {
                if (completeResult.redirectUrl) {
                    // Use the redirect URL from the server response (for Index page)
                    window.location.href = completeResult.redirectUrl;
                } else {
                    // Default redirect for Account/Login page
                    alert('Login successful!');
                    window.location.href = '/Home';
                }
            } else {
                alert('Login failed: ' + completeResult.errorMessage);
            }
        } catch (err) {
            console.error('Login error:', err);
            alert('An error occurred during login. Details: ' + err.message);
        }
    });
}