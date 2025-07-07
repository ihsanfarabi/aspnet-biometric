// FIDO2 client-side script

function arrayBufferToBase64Url(buffer) {
    let binary = '';
    const bytes = new Uint8Array(buffer);
    const len = bytes.byteLength;
    for (let i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
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
                alert('Registration successful!');
                window.location.href = '/';
            } else {
                alert('Registration failed: ' + completeResult.errorMessage);
            }
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
                window.location.href = '/';
            } else {
                alert('Login failed: ' + completeResult.errorMessage);
            }
        } catch (err) {
            console.error('Login error:', err);
            alert('An error occurred during login. Details: ' + err.message);
        }
    });
}