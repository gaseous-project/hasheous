async function GetPrivilegedUsers() {
    await fetch('/api/v1/AccountAdmin/Users', {
        method: 'GET'
    }).then(async response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return await response.json();
    }).then(success => {
        let adminUsers = [];
        let moderatorUsers = [];

        for (let i = 0; i < success.length; i++) {
            adminUsers.push(success[i]);
        }

        buildRoleTable(adminUsers, 'roles_admins');
    }).catch(error => {
        console.error('Error fetching privileged users:', error);
        document.getElementById('roles_admins').innerHTML = '<p>Error loading admin users.</p>';
    });
}

function buildRoleTable(roleList, targetDiv) {
    console.log(roleList);

    let tableContents = [];
    roleList.forEach(element => {
        let userRoles = '';
        element.roles.forEach(role => {
            let roleColourHash = hashCode(role.toLowerCase());
            let roleColour = intToRGB(roleColourHash);
            userRoles += "<span class='badge' style='background-color:#" + roleColour + "'>" + lang.getLang(role) + "</span>";
        });

        let row = {
            id: element.id,
            emailAddress: element.emailAddress,
            roles: userRoles
        };
        tableContents.push(row);
    });

    let targetDivObj = document.getElementById(targetDiv);
    targetDivObj.innerHTML = '';
    targetDivObj.appendChild(new generateTable(
        tableContents,
        ['id', 'emailAddress', 'roles'],
        'id',
        true,
        async function (id, resultSet) {
            for (var i = 0; i < resultSet.length; i++) {
                if (resultSet[i].id == id) {
                    document.getElementById('role_email').value = resultSet[i].emailAddress;
                    await loadUserRoles();
                    break;
                }
            }
        }
    ));
}

async function LoadRoles() {
    await fetch('/api/v1/AccountAdmin/Roles', {
        method: 'GET'
    }).then(async response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return await response.json();
    }).then(success => {
        let roleSelectorDiv = document.getElementById('role_selector');
        roleSelectorDiv.innerHTML = '';

        success.forEach(role => {
            if (role.allowManualAssignment) {
                // create a checkbox for this role
                let checkboxId = 'role_checkbox_' + role.name;

                let checkboxWrapper = document.createElement('div');
                checkboxWrapper.className = 'checkboxwrapper';

                let checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.id = checkboxId;
                checkbox.value = role.name;

                let label = document.createElement('label');
                label.htmlFor = checkboxId;
                label.appendChild(document.createTextNode(lang.getLang(role.name)));

                checkboxWrapper.appendChild(checkbox);
                checkboxWrapper.appendChild(label);

                roleSelectorDiv.appendChild(checkboxWrapper);
            }
        });
    }).catch(error => {
        console.error('Error loading roles:', error);
    });
}

// user load button handler
async function loadUserRoles() {
    let email = document.getElementById('role_email').value;
    let roleUserDiv = document.getElementById('role_userRoles');
    roleUserDiv.style.display = 'none';
    let roleErrorDiv = document.getElementById('role_error');
    roleErrorDiv.style.display = 'none';

    if (!email || email.trim() === '') {
        roleErrorDiv.style.display = 'table-row';
        return;
    }

    await fetch('/api/v1/AccountAdmin/Users/' + encodeURIComponent(email), {
        method: 'GET'
    }).then(async response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return await response.json();
    }).then(success => {
        console.log(success);
        if (success) {
            roleUserDiv.style.display = 'table-row';

            // clear all checkboxes
            let checkboxes = document.querySelectorAll('#role_selector input[type="checkbox"]');
            checkboxes.forEach(checkbox => {
                checkbox.checked = false;
            });

            // set checkboxes based on user roles
            success.roles.forEach(role => {
                let checkbox = document.querySelector('#role_selector input[type="checkbox"][value="' + role + '"]');
                if (checkbox) {
                    checkbox.checked = true;
                }
            });
        }
    }).catch(error => {
        roleErrorDiv.style.display = 'table-row';
        console.error('Error loading user roles:', error);
        // clear all checkboxes
        let checkboxes = document.querySelectorAll('#role_selector input[type="checkbox"]');
        checkboxes.forEach(checkbox => {
            checkbox.checked = false;
        });
    });
};
document.getElementById('role_load').addEventListener('click', async function () {
    await loadUserRoles();
});

// apply role button handler
document.getElementById('role_apply').addEventListener('click', async function () {
    let roleUserDiv = document.getElementById('role_userRoles');
    roleUserDiv.style.display = 'none';
    let roleErrorDiv = document.getElementById('role_error');
    roleErrorDiv.style.display = 'none';

    let email = document.getElementById('role_email').value;

    let checkboxes = document.querySelectorAll('#role_selector input[type="checkbox"]');
    let roleList = [];
    checkboxes.forEach(checkbox => {
        if (checkbox.checked) {
            roleList.push(checkbox.value);
        }
    });

    // send role list to server
    await postData(
        '/api/v1/AccountAdmin/Users/' + encodeURIComponent(email) + '/Roles',
        'POST',
        roleList,
        true  // authenticated
    ).then(async response => {
        if (response.ok) {
            document.getElementById('role_email').value = '';
            GetPrivilegedUsers();
        } else {
            throw new Error('Network response was not ok');
        }
    }).catch(error => {
        console.error('Error applying roles:', error);
        document.getElementById('role_email').value = '';
        GetPrivilegedUsers();
    });
});

LoadRoles();
GetPrivilegedUsers();

// Flush Cache button handler
document.getElementById('flushcache').addEventListener('click', function () {
    postData(
        '/api/v1.0/BackgroundTasks/Cache/Flush',
        'POST',
        {},
        true
    ).then(response => {
        if (response.ok) {
            alert('Cache flushed successfully.');
        } else {
            throw new Error('Failed to flush cache');
        }
    }).catch(error => {
        console.error('Error flushing cache:', error);
        alert('Error flushing cache.');
    });
});