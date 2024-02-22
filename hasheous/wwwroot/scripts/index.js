// set up banner UI elements
$('#banner_search_field').select2({
    placeholder: "Search",
    allowClear: true
});

// user menu drop down menu
function showMenu() {
    document.getElementById("myDropdown").classList.toggle("show");
}

// Close the dropdown menu if the user clicks outside of it
window.onclick = function(event) {
    if (!event.target.matches('.dropbtn')) {
        var dropdowns = document.getElementsByClassName("dropdown-content");
        var i;
        for (i = 0; i < dropdowns.length; i++) {
            var openDropdown = dropdowns[i];
            if (openDropdown.classList.contains('show')) {
                openDropdown.classList.remove('show');
            }
        }
    }
}

// handle logins
var buttonProfile = document.getElementById('banner_user');;
var buttonLogin = document.getElementById('banner_login');
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

// load the page into the main container and set the page title
var targetPage = getQueryString('page', 'string');
if (!targetPage) { targetPage = "home"; }
switch (targetPage) {
    default:
        $('#content').load('/pages/' + targetPage + '.html');
        if (pageNames[targetPage]) {
            if (pageNames[targetPage].length > 0) {
                document.title = "Hasheous - " + pageNames[targetPage];
            } else {
                document.title = "Hasheous";    
            }
        } else {
            document.title = "Hasheous";
        }
        break;
}
var pageScriptDiv = document.getElementById('postLoadPageScripts');
pageScriptDiv.innerHTML = '';
var pageScriptElement = document.createElement('script');
pageScriptElement.setAttribute('src', '/pages/' + targetPage + '.js');
pageScriptDiv.appendChild(pageScriptElement);