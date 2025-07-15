const login = new loginTools();

// setup email address field
let forgottenEmailField = document.getElementById('forgottenpasswordemail');
forgottenEmailField.pattern = login.emailRegex;
forgottenEmailField.addEventListener("keyup", function (e) {
    this.classList.remove('valid-border-color');
    this.classList.remove('invalid-border-color');

    let sendLinkButton = document.getElementById('forgottenpasswordsendlink');

    if (login.isValidEmail(this.value) == true) {
        this.classList.add('valid-border-color');
        sendLinkButton.removeAttribute('disabled');
    } else {
        this.classList.add('invalid-border-color');
        sendLinkButton.setAttribute('disabled', 'disabled');
    }
});

// setup send link button
let forgottenPasswordPanel = document.getElementById('forgottenpassword');
let forgottenPasswordSentLinkPanel = document.getElementById('forgottenpasswordsentlink');
document.getElementById('forgottenpasswordsendlink').addEventListener('click', function (e) {
    let model = {
        "email": forgottenEmailField.value
    };

    postData(
        '/api/v1/Account/ForgotPassword',
        'POST',
        model
    ).then(async response => {
        if (response.ok) {
            forgottenPasswordPanel.style.display = 'none';
            forgottenPasswordSentLinkPanel.style.display = '';
        } else {
            response.json().then(error => {
                console.error('Error:', error);
                alert('Failed to send password reset link: ' + error.message);
            });
        }
    }).catch(error => {
        console.error('Error:', error);
        alert('An error occurred while sending the password reset link.');
    });
});