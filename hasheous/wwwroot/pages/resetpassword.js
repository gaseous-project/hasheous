const login = new loginTools();

let emailValidated = false;
let passwordValidated = false;
let resetPasswordButton = document.getElementById('resetpasswordsubmit');

// handle error messages
function handleErrors() {
    let errorlabel = document.getElementById('resetpassword_errorlabel');
    errorlabel.innerHTML = '';

    if (login.emailStatus != undefined) {
        let emailError = document.createElement('div');
        switch (login.emailStatus.level) {
            case 1:
                // text should be red
                emailError.innerHTML = lang.getLang(login.emailStatus.label);
                emailError.classList.add('errorlabel-error');
                errorlabel.appendChild(emailError);
                break;

            default:
                // no error
                break;
        }
    }

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

    if (emailValidated == true && passwordValidated == true) {
        resetPasswordButton.removeAttribute('disabled');
    } else {
        resetPasswordButton.setAttribute('disabled', 'disabled');
    }
}

// setup email address field
let resetPasswordEmailField = document.getElementById('resetpassword_email');
resetPasswordEmailField.pattern = login.emailRegex;
resetPasswordEmailField.addEventListener("keyup", function (e) {
    this.classList.remove('valid-border-color');
    this.classList.remove('invalid-border-color');
    
    if (login.isValidEmail(this.value) == true) {
        this.classList.add('valid-border-color');
        emailValidated = true;
    } else {
        this.classList.add('invalid-border-color');
        emailValidated = false;
    }
    handleErrors();
});

// setup password fields
let passwordField = document.getElementById('resetpassword_newpassword');
let passwordConfirmField = document.getElementById('resetpassword_newconfirmpassword');
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

// check for query strings
let user = new URLSearchParams(window.location.search).get('userId');
let code = new URLSearchParams(window.location.search).get('code');

if (user && code) {
    let resetPasswordButton = document.getElementById('resetpasswordsubmit');
    resetPasswordButton.addEventListener("click", function (e) {
        let newPasswordField = document.getElementById('resetpassword_newpassword');
        let newPasswordConfirmField = document.getElementById('resetpassword_newconfirmpassword');
        let ajaxData = {
            "Email": resetPasswordEmailField.value,
            "Code": code,
            "Password": newPasswordField.value,
            "ConfirmPassword": newPasswordConfirmField.value
        };

        ajaxCall(
            '/api/v1/Account/ResetPassword',
            'POST',
            function(result) {
                resetPasswordCallback(result);
            },
            function(error) {
                resetPasswordCallback(error);
            },
            JSON.stringify(ajaxData)
        );
    });
} else {
    window.location.replace('/');
}

function resetPasswordCallback(result) {
    window.location.replace('/?page=resetpasswordconfirmed');
}

