let totalTime = 60;
let timeoutFunc;

function countDownTimer() {
    if (totalTime == 0) {
        window.location.replace('/');
    } else {
        document.getElementById('changepasswordconfirmationcomplete').innerHTML = lang.getLang('changepasswordconfirmationcomplete', [ totalTime, '<a href="#" onclick="window.location.replace(\'/index.html?page=account\');">' + lang.getLang('profile') + '</a>' ]);
        totalTime = totalTime - 1;
        setTimeout(countDownTimer, 1000);
    }
}

countDownTimer();