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
        
        try {
            console.log('Making request to ToggleBiometric handler...');
            
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

            console.log('Response status:', response.status);
            console.log('Response ok:', response.ok);
            console.log('Response url:', response.url);
            
            if (!response.ok) {
                if (response.status === 302) {
                    throw new Error('Authentication failed. Please refresh the page and try again.');
                }
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const result = await response.json();
            console.log('Toggle biometric response:', result);

            if (result.status === 'ok') {
                // Successfully disabled
                console.log('Successfully toggled, enabled:', result.enabled);
                updateBiometricStatus(result.enabled);
                alert(result.message);
            } else if (result.status === 'register') {
                // Need to register biometric credential
                console.log('Starting registration process...');
                await registerBiometricCredential(result.options);
            } else {
                // Error occurred
                console.error('Server returned error:', result.errorMessage);
                toggle.checked = !isEnabled; // Revert toggle
                alert('Error: ' + result.errorMessage);
            }
        } catch (err) {
            console.error('Toggle biometric error:', err);
            toggle.checked = !isEnabled; // Revert toggle
            alert('An error occurred: ' + err.message);
        }
    });
} else {
    console.log('Biometric toggle element not found!');
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
            statusText.textContent = 'Disabled - Use password to login';
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

            const response = await fetch(`/Account/Login?handler=GetAssertionOptionsUsernameless`, {
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

            const completeResponse = await fetch('/Account/Login?handler=MakeAssertion', {
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
                alert('Login successful!');
                window.location.href = '/Home';
            } else {
                alert('Login failed: ' + completeResult.errorMessage);
            }
        } catch (err) {
            console.error('Login error:', err);
            alert('An error occurred during login. Details: ' + err.message);
        }
    });
}