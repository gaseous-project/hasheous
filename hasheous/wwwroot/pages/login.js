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

    ajaxCall(
        '/api/v1/Account/Login',
        'POST',
        function(result) {
            loginCallback(result);
        },
        function(error) {
            loginCallback(error);
        },
        JSON.stringify(loginObj)
    );

    function loginCallback(result) {
        switch(result.status) {
            case 200:
                window.location.replace('/index.html');
                break;
            default:
                // login failed
                document.getElementById('login_errorlabel').innerHTML = 'Incorrect password';
                break;
        }
    }
}