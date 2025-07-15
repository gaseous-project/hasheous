let pageType = getQueryString('type', 'string');
document.getElementById('dataObject_cancel').addEventListener("click", function (e) {
    window.location = '/index.html?page=dataobjects&type=' + pageType;
});
setPageTitle("newcompany");

let mustRedirect = true;
if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Admin') || userProfile.Roles.includes('Moderator')) {
            mustRedirect = false;
        }
    }
}

if (mustRedirect == true) { location.window.replace("/"); }

function saveDataObject() {
    let model = {
        "name": document.getElementById('dataObject_object_name').value
    }

    postData(
        '/api/v1/DataObjects/' + pageType,
        'POST',
        model,
        true
    ).then(async response => {
        if (response.ok) {
            console.log(response);
            let jsonResponse = await response.json();
            window.location = '/index.html?page=dataobjectdetail&type=' + pageType + '&id=' + jsonResponse.id;
        } else {
            response.json().then(error => {
                console.error('Error:', error);
                alert('Failed to save data object: ' + error.message);
            });
        }
    }).catch(error => {
        console.error('Error:', error);
        alert('An error occurred while saving the data object.');
    });
}