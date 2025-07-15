// redirect home if the user is logged in already
if (userProfile != null) {
    window.location.replace('/');
}

function UserLogin() {
    let loginObj = {
        "email": document.getElementById('login_email').value,
        "password": document.getElementById('login_password').value,
        "rememberMe": document.getElementById('login_rememberme').checked
    }

    postData(
        '/api/v1/Account/Login',
        'POST',
        loginObj,
        true // return result to handle response
    )
        .then(result => {
            loginCallback(result);
        })
        .catch(error => {
            console.error('Error during login:', error);
            document.getElementById('login_errorlabel').innerHTML = lang.getLang('incorrectpassword');
        });

    function loginCallback(result) {
        switch (result.status) {
            case 200:
                window.location.replace('/index.html');
                break;
            default:
                // login failed
                document.getElementById('login_errorlabel').innerHTML = lang.getLang('incorrectpassword');
                break;
        }
    }
}

// check if social login buttons should be displayed
fetch('/api/v1/Account/social-login', {
    method: 'GET',
    headers: {
        'Content-Type': 'application/json'
    }
})
    .then(response => response.json())
    .then(data => {
        if (data.includes('Google')) {
            document.getElementById('social_login_button_google').style.display = 'table-row';
        }
        if (data.includes('Microsoft')) {
            document.getElementById('social_login_button_microsoft').style.display = 'table-row';
        }
    })
    .catch(error => {
        console.error('Error fetching social login options:', error);
    });

function SocialLogin(provider) {
    switch (provider) {
        case 'google':
            window.location.href = '/api/v1.0/Account/signin-google';
            break;
        case 'microsoft':
            window.location.href = '/api/v1.0/Account/signin-microsoft';
            break;
        default:
            console.error('Unsupported social login provider:', provider);
            break;
    }
}
// end of social login functionality