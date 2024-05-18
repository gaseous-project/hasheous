class loginTools {
    minimumPasswordLength = 12;
    maximumPasswordLength = 128;

    emailRegex = /^(([^<>()[\]\.,;:\s@\"]+(\.[^<>()[\]\.,;:\s@\"]+)*)|(\".+\"))@(([^<>()[\]\.,;:\s@\"]+\.)+[^<>()[\]\.,;:\s@\"]{2,})$/i;

    #emailValidationReturns = [
        {
            "level": 0,
            "label": 'emailvalid'
        },
        {
            "level": 1,
            "label": "emailinvalid"
        }
    ];

    #credentialValidationReturns = [
        {
            "level": 0,
            "label": 'passwordvalid'
        },
        {
            "level": 1,
            "label": 'passwordtooshort',
            "substitutes": this.minimumPasswordLength
        },
        {
            "level": 1,
            "label": 'passwordtoolong',
            "substitutes": this.maximumPasswordLength
        },
        {
            "level": 0,
            "label": 'passwordsdontmatchquiet'
        },
        {
            "level": 1,
            "label": 'passwordsdontmatch'
        },
        {
            "level": 1,
            "label": 'passwordnotcomplex'
        }
    ];

    emailStatus = undefined;

    passwordStatus = undefined;
    passwordStrength = 0;

    isValidEmail(emailAddress) {
        if (emailAddress == '' || !this.emailRegex.test(emailAddress)) {
            this.emailStatus = this.#emailValidationReturns[1];
            return false;
        } else {
            this.emailStatus = this.#emailValidationReturns[0];
            return true;
        }
    }

    isPasswordValid(passwordString, confirmPasswordString) {
        if (
            passwordString.length >= this.minimumPasswordLength &&
            passwordString.length < this.maximumPasswordLength
        ) {
            // password is the right length - check complexity
            let containsLowerCase = false;
            let containsUpperCase = false;
            let containsNumbers = false;
            let containsSpecialChars = false;

            if (passwordString.match(/[a-z]+/)) {
                this.passwordStrength += 1;
                containsLowerCase = true;
            }
            if (passwordString.match(/[A-Z]+/)) {
                this.passwordStrength += 1;
                containsUpperCase = true;
            }
            if (passwordString.match(/[0-9]+/)) {
                this.passwordStrength += 1;
                containsNumbers = true;
            }
            if (passwordString.match(/[$@#&!]+/)) {
                this.passwordStrength += 1;
                containsSpecialChars = true;
            }

            if (
                containsLowerCase == false ||
                containsUpperCase == false ||
                containsNumbers == false ||
                containsSpecialChars == false
            ) {
                this.passwordStatus = this.#credentialValidationReturns[5];
                return false;
            }

            // password is the right length and complexity - check it matches the confirmation password
            if (passwordString != confirmPasswordString) {
                if (confirmPasswordString.length == 0) {
                    // no confirmation password, return a quiet non-match message
                    this.passwordStatus = this.#credentialValidationReturns[3];
                    return false;
                } else {
                    // passwords don't match, return a louder non-match message
                    this.passwordStatus = this.#credentialValidationReturns[4];
                    return false;
                }
            } else {
                // password seems good!
                this.passwordStatus = this.#credentialValidationReturns[0];
                return true;
            }
        } else if (passwordString.length < this.minimumPasswordLength) {
            // password is too short
            this.passwordStatus = this.#credentialValidationReturns[1];
            return false;
        } else if (passwordString.length >= this.maximumPasswordLength) {
            // password is too long
            this.passwordStatus = this.#credentialValidationReturns[2];
            return false;
        }
    }
}