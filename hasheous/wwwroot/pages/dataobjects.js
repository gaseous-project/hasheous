let pageType = getQueryString('type', 'string');

// determine which buttons should be displayed based on page and user roles
let showNewButton = false;

if (userProfile != null && userProfile.Roles != null) {
    switch (pageType) {
        case 'game':
            showNewButton = userProfile.Roles.includes('Admin') || userProfile.Roles.includes('Moderator');
            break;
        case 'company':
            showNewButton = userProfile.Roles.includes('Admin') || userProfile.Roles.includes('Moderator');
            break;
        case 'platform':
            showNewButton = userProfile.Roles.includes('Admin');
            break;
        case 'app':
            showNewButton = userProfile.Roles.includes('Admin') || userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Member');
            break;
        default:
            showNewButton = false;
    }
}

if (showNewButton) {
    let newObjectBtn = document.getElementById('dataObjectNew');
    newObjectBtn.style.display = '';
    newObjectBtn.addEventListener("click", function (e) {
        window.location = '/index.html?page=dataobjectnew&type=' + pageType;
    });
}

function setupPage() {
    setPageTitle(lang.langMapping[pageType]['dataobject_objects']);
}

setupPage();
createDataObjectsTable(1, 20, pageType);
