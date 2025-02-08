let pageType = getQueryString('type', 'string');

function setupPage() {
    if (userProfile != null) {
        if (userProfile.Roles.includes("Admin")) {
            // show admin tools
            let newObjectBtn = document.getElementById('dataObjectNew');
            newObjectBtn.style.display = '';
            newObjectBtn.addEventListener("click", function (e) {
                window.location = '/index.html?page=dataobjectnew&type=' + pageType;
            });
        }
    }

    setPageTitle(lang.langMapping[pageType]['dataobject_objects']);
}

setupPage();
createDataObjectsTable(1, 20, pageType);
