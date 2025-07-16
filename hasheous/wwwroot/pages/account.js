const login = new loginTools();

// setup roles
let roleBadgeDiv = document.getElementById('account_roles');
for (let i = 0; i < userProfile.Roles.length; i++) {
    let roleName = userProfile.Roles[i].toLowerCase();
    console.log(roleName);
    let roleBadge = document.createElement('div');
    roleBadge.innerHTML = lang.getLang(roleName);
    roleBadge.classList.add('badge');
    roleBadgeDiv.appendChild(roleBadge);
}

// setup submission api key
let submissionApiKeyField = document.getElementById('submissionapikey');
function GetApiKey() {
    fetch('/api/v1/Account/APIKey', {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (response.ok) {
            return response.text();
        } else {
            throw new Error('Failed to fetch API key');
        }
    }).then(apiKey => {
        submissionApiKeyField.value = apiKey;
    }).catch(error => {
        submissionApiKeyField.value = error.message;
    });
}
GetApiKey();

// setup submission api key reset
document.getElementById('submissionapikeyresetbutton').addEventListener('click', function (e) {
    postData('/api/v1/Account/APIKey', 'POST', {}, true)
        .then(data => {
            // Handle success
            console.log('API Key reset successfully:', data);
            GetApiKey();
        })
        .catch(error => {
            // Handle error
            console.error('Error resetting API Key:', error);
            GetApiKey();
        });
});

// setup password change
let errorlabel = document.getElementById('changepassword_errorlabel');
let passwordValidated = false;
let resetPasswordButton = document.getElementById('changepasswordsubmit');

// handle error messages
function handleErrors() {
    errorlabel.innerHTML = '';

    if (login.passwordStatus != undefined) {
        let passwordError = document.createElement('div');
        switch (login.passwordStatus.level) {
            case 1:
                // text should be red
                let substitutes;
                if (login.passwordStatus.substitutes) {
                    substitutes = login.passwordStatus.substitutes;
                }
                passwordError.innerHTML = lang.getLang(login.passwordStatus.label, substitutes);
                passwordError.classList.add('errorlabel-error');
                errorlabel.appendChild(passwordError);
                break;

            default:
                // no error
                break;
        }
    }

    if (passwordValidated == true) {
        resetPasswordButton.removeAttribute('disabled');
    } else {
        resetPasswordButton.setAttribute('disabled', 'disabled');
    }
}

// setup password fields
let passwordField = document.getElementById('changepassword_newpassword');
let passwordConfirmField = document.getElementById('changepassword_newconfirmpassword');
function validatePassword(e) {
    passwordField.classList.remove('valid-border-color');
    passwordConfirmField.classList.remove('valid-border-color');
    passwordField.classList.remove('invalid-border-color');
    passwordConfirmField.classList.remove('invalid-border-color');

    if (login.isPasswordValid(passwordField.value, passwordConfirmField.value) == true) {
        passwordField.classList.add('valid-border-color');
        passwordConfirmField.classList.add('valid-border-color');
        passwordValidated = true;
    } else {
        passwordField.classList.add('invalid-border-color');
        passwordConfirmField.classList.add('invalid-border-color');
        passwordValidated = false;
    }
    handleErrors();
}
passwordField.addEventListener("keyup", function (e) {
    validatePassword();
});
passwordConfirmField.addEventListener("keyup", function (e) {
    validatePassword();
});

// setup change password button
resetPasswordButton.addEventListener("click", function (e) {
    let oldPasswordField = document.getElementById('changepassword_oldpassword');
    let newPasswordField = document.getElementById('changepassword_newpassword');
    let newPasswordConfirmField = document.getElementById('changepassword_newconfirmpassword');
    let ajaxData = {
        "OldPassword": oldPasswordField.value,
        "NewPassword": newPasswordField.value,
        "ConfirmPassword": newPasswordConfirmField.value
    };

    postData('/api/v1/Account/ChangePassword', 'POST', ajaxData, true)
        .then(result => {
            resetPasswordCallback(result, '/?page=changepasswordconfirmed');
        })
        .catch(error => {
            resetPasswordCallback(error, '/?page=changepasswordconfirmed');
        });
});

function resetPasswordCallback(result, redirectPath) {
    console.log(result);
    switch (result.status) {
        case 200:
            window.location.replace(redirectPath);
            break;

        default:
            errorlabel.innerHTML = '';
            if (result.responseJSON) {
                if (result.responseJSON.errors) {
                    for (let i = 0; i < result.responseJSON.errors.length; i++) {
                        let error = document.createElement('span');
                        error.innerHTML = lang.getLang(result.responseJSON.errors[i].code);
                        errorlabel.appendChild(error);
                    }
                }
            }

            break;

    }
}

// setup linked account buttons
fetch('/api/v1/Account/social-login', {
    method: 'GET',
    headers: {
        'Content-Type': 'application/json'
    }
})
    .then(response => response.json())
    .then(async data => {
        if (data.length > 0) {
            document.getElementById('linkedaccounts').style.display = 'block';

            let linkedAccounts = await fetch('/api/v1/Account/linked-logins', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            linkedAccounts = await linkedAccounts.json();

            let linkedAccountsList = document.getElementById('linkedaccountslist');
            linkedAccountsList.innerHTML = ''; // clear existing entries
            data.forEach(account => {
                let accountRow = document.createElement('tr');

                let accountCellIcon = document.createElement('td');
                accountCellIcon.style.width = '30px';
                let accountIcon = document.createElement('img');
                switch (account) {
                    case "Google":
                        accountIcon.src = '/images/google-signin-logo.svg';
                        break;
                    case "Microsoft":
                        accountIcon.src = '/images/ms-signin-logo.svg';
                        break;
                    default:
                        accountIcon.src = '/images/social-signin-logo.svg';
                        break;
                }
                accountIcon.alt = account;
                accountIcon.style.width = '24px';
                accountIcon.style.height = '24px';
                accountCellIcon.appendChild(accountIcon);
                accountRow.appendChild(accountCellIcon);

                let accountCellName = document.createElement('td');
                accountCellName.innerHTML = lang.getLang(account.toLowerCase());
                accountRow.appendChild(accountCellName);

                let accountCellButton = document.createElement('td');
                let accountButton = document.createElement('button');
                accountButton.setAttribute('type', 'button');
                let linkedAccount = linkedAccounts.find(a => a.loginProvider === account);
                if (linkedAccount) {
                    accountButton.innerHTML = lang.getLang('unlink');
                    accountButton.classList.add('redbutton');
                    accountButton.addEventListener('click', () => {
                        window.location.href = `/api/v1/Account/unlink-login/${account}?returnUrl=/index.html?page=account`;
                    });
                } else {
                    accountButton.innerHTML = lang.getLang('link');
                    accountButton.addEventListener('click', () => {
                        startExternalLogin(account);
                        // window.location.href = `/api/v1/Account/link-login/${account}?returnUrl=/index.html?page=account`;
                    });
                }
                accountCellButton.appendChild(accountButton);
                accountRow.appendChild(accountCellButton);

                linkedAccountsList.appendChild(accountRow);
            });
        }
    });

// setup delete account confirmation checkbox
let deleteAccountButton = document.getElementById('deleteaccount');
document.getElementById('deleteaccountconfirm').addEventListener("change", function (e) {
    if (this.checked == true) {
        deleteAccountButton.removeAttribute('disabled');
    } else {
        deleteAccountButton.setAttribute('disabled', 'disabled');
    }
});

// setup delete button
deleteAccountButton.addEventListener("click", function (e) {
    postData('/api/v1/Account/Delete', 'POST', {}, true)
        .then(success => {
            resetPasswordCallback(success, '/');
        })
        .catch(error => {
            resetPasswordCallback(error, '/');
        });
});

async function startExternalLogin(provider) {
    // Replace with your API base URL and version as needed
    window.location.href = `/api/v1.0/Account/link-login/${encodeURIComponent(provider)}?returnUrl=${encodeURIComponent('/index.html?page=account')}`;
}