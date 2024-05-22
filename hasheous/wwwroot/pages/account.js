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
    ajaxCall(
        '/api/v1/Account/APIKey',
        'GET',
        function (success) {
            submissionApiKeyField.value = success;
        },
        function (error) {
            submissionApiKeyField.value = error.responseText;
        }
    );
}
GetApiKey();

// setup submission api key reset
document.getElementById('submissionapikeyresetbutton').addEventListener('click', function (e) {
    ajaxCall(
        '/api/v1/Account/APIKey',
        'POST',
        function (success) {
            GetApiKey();
        },
        function (error) {
            GetApiKey();
        }
    );
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

    ajaxCall(
        '/api/v1/Account/ChangePassword',
        'POST',
        function (result) {
            resetPasswordCallback(result, '/?page=changepasswordconfirmed');
        },
        function (error) {
            resetPasswordCallback(error, '/?page=changepasswordconfirmed');
        },
        JSON.stringify(ajaxData)
    );
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
    ajaxCall(
        '/api/v1/Account/Delete',
        'POST',
        function (success) {
            resetPasswordCallback(success, '/');
        },
        function (error) {
            resetPasswordCallback(error, '/');
        }
    );
})