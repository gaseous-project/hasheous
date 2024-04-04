// set up banner UI elements
let searchBox = document.getElementById('banner_search_field');
searchBox.placeholder = lang.getLang("search");
searchBox.addEventListener("keypress", function(e) {
    let key = e.code;
    if (key == 'Enter') {
        e.preventDefault();
        window.location.href = 'https://localhost:7157/index.html?page=search&query=' + encodeURIComponent(searchBox.value);
    }
});

// user menu drop down menu
function showMenu() {
    document.getElementById("myDropdown").classList.toggle("show");
}

// Close the dropdown menu if the user clicks outside of it
window.onclick = function(event) {
    if (!event.target.matches('.dropbtn')) {
        let dropdowns = document.getElementsByClassName("dropdown-content");
        for (let i = 0; i < dropdowns.length; i++) {
            let openDropdown = dropdowns[i];
            if (openDropdown.classList.contains('show')) {
                openDropdown.classList.remove('show');
            }
        }
    }
}

// handle logins
let buttonProfile = document.getElementById('banner_user');;
let buttonLogin = document.getElementById('banner_login');
if (userProfile == null) {
    // not logged in
    buttonProfile.style.display = 'none';
    buttonLogin.style.display = '';
} else {
    buttonProfile.style.display = '';
    buttonLogin.style.display = 'none';
}

function userLogoff() {
    ajaxCall(
        '/api/v1/Account/LogOff',
        'POST',
        function (result) {
            location.replace("/index.html");
        },
        function (error) {
            location.replace("/index.html");
        }
    );
}

function setPageElementInnerHTMLLanguage(elementList) {
    for (let i = 0; i < elementList.length; i++) {
        if (elementList[i].getAttribute('data-lang')) {
            elementList[i].innerHTML = lang.getLang(elementList[i].getAttribute('data-lang'));
        }
    }
}

// load the page into the main container and set the page title
let targetPage = getQueryString('page', 'string');
if (!targetPage) { targetPage = "home"; }
switch (targetPage) {
    default:
        $('#content').load('/pages/' + targetPage + '.html', function(responseTxt, statusTxt, xhr){
            if (statusTxt == "success") {
                let pageScriptDiv = document.getElementById('postLoadPageScripts');
                pageScriptDiv.innerHTML = '';
                let pageScriptElement = document.createElement('script');
                pageScriptElement.setAttribute('src', '/pages/' + targetPage + '.js');
                pageScriptDiv.appendChild(pageScriptElement);

                // set page headers
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('h1'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('h2'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('span'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('p'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('div'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('th'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('td'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('label'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('a'));
                setPageElementInnerHTMLLanguage(document.getElementsByTagName('button'));
            }
            if (statusTxt == "error") {
                console.error("Error loading page: " + xhr.status + ": " + xhr.statusText);
            }
            setPageTitle(targetPage);
        });
        break;
}

function setPageTitle(targetPage, overrideLanguageLookup) {
    if (overrideLanguageLookup === true) {
        document.title = lang.getLang("Hasheous") + " - " + targetPage;
    } else {
        if (lang.getLang(targetPage)) {
            if (lang.getLang(targetPage).length > 0) {
                document.title = lang.getLang("Hasheous") + " - " + lang.getLang(targetPage);
            } else {
                document.title = lang.getLang("Hasheous");
            }
        } else {
            document.title = lang.getLang("Hasheous");
        }
    }
}