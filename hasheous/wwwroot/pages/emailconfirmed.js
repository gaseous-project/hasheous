let totalTime = 60;
let timeoutFunc;

function countDownTimer() {
    if (totalTime == 0) {
        window.location.replace('/');
    } else {
        document.getElementById('emailconfirmationcomplete').innerHTML = lang.getLang('emailconfirmationcomplete', [ totalTime, '<a href="#" onclick="window.location.replace(\'/\');">' + lang.getLang('home') + '</a>' ]);
        totalTime = totalTime - 1;
        setTimeout(countDownTimer, 1000);
    }
}

countDownTimer();