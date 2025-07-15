const login = new loginTools();
let emailValidated = false;
let passwordValidated = false;

// redirect home if the user is logged in already
if (userProfile != null) {
    window.location.replace('/');
}

// handle error messages
function handleErrors() {
    let errorlabel = document.getElementById('register_errorlabel');
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
        registerButton.removeAttribute('disabled');
    } else {
        registerButton.setAttribute('disabled', 'disabled');
    }
}

// setup email address field
let registerEmailField = document.getElementById('register_email');
registerEmailField.pattern = login.emailRegex;
registerEmailField.addEventListener("keyup", function (e) {
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
let passwordField = document.getElementById('register_password');
let passwordConfirmField = document.getElementById('register_confirmpassword');
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

// setup register button
let registerButton = document.getElementById('registernewaccount');
registerButton.addEventListener("click", function (e) {
    let loginModel = {
        "userName": document.getElementById('register_email').value,
        "email": document.getElementById('register_email').value,
        "password": document.getElementById('register_password').value,
        "confirmPassword": document.getElementById('register_confirmpassword').value
    };

    // verify email is an email
    if (login.isValidEmail(loginModel.email) == false) {
        // failed testing - throw an error
        handleErrors();
        return false;
    } else {
        // email good - check password
        if (login.isPasswordValid(loginModel.password, loginModel.confirmPassword)) {
            // password is good too - submit

            postData(
                '/api/v1/Account/Register',
                'POST',
                loginModel,
                false // return result
            )
                .then(data => {
                    processRegistration(data);
                })
                .catch(error => {
                    processRegistration(error);
                });
        } else {
            handleErrors();
            return false;
        }
    }
});

function processRegistration(message) {
    console.log(message);
    if (message.succeeded == true) {
        document.getElementById('registerpanel').style.display = 'none';
        document.getElementById('registrationcomplete').style.display = '';
    } else {

    }
}