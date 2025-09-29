function GetPrivilegedUsers() {
    fetch('/api/v1/AccountAdmin/Users', {
        method: 'GET'
    }).then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    }).then(success => {
        let adminUsers = [];
        let moderatorUsers = [];

        for (let i = 0; i < success.length; i++) {
            switch (success[i].highestRole) {
                case 'Admin':
                    adminUsers.push(success[i]);
                    break;

                case 'Moderator':
                    moderatorUsers.push(success[i]);
                    break;
            }
        }

        buildRoleTable(adminUsers, 'roles_admins');
        buildRoleTable(moderatorUsers, 'roles_moderators');
    }).catch(error => {
        console.error('Error fetching privileged users:', error);
        document.getElementById('roles_admins').innerHTML = '<p>Error loading admin users.</p>';
        document.getElementById('roles_moderators').innerHTML = '<p>Error loading moderator users.</p>';
    });
}

function buildRoleTable(roleList, targetDiv) {
    let targetDivObj = document.getElementById(targetDiv);
    targetDivObj.innerHTML = '';
    targetDivObj.appendChild(new generateTable(
        roleList,
        ['id', 'emailAddress'],
        'id',
        true,
        function (id, resultSet) {
            for (var i = 0; i < resultSet.length; i++) {
                if (resultSet[i].id == id) {
                    document.getElementById('role_email').value = resultSet[i].emailAddress;
                    $('#role_type').val(resultSet[i].highestRole).trigger('change');
                    break;
                }
            }
        }
    ));
}

document.getElementById('role_apply').addEventListener('click', function () {
    let email = document.getElementById('role_email').value;
    let role = document.getElementById('role_type').value;

    postData(
        '/api/v1/AccountAdmin/Users/' + email + '/Roles?RoleName=' + role,
        'POST',
        {},
        true
    ).then(response => {
        if (response.ok) {
            document.getElementById('role_email').value = '';
            $('#role_type').val('None').trigger('change');
            GetPrivilegedUsers();
        } else {
            throw new Error('Failed to apply role');
        }
    }).catch(error => {
        console.error('Error applying role:', error);
        document.getElementById('role_email').value = '';
        $('#role_type').val('None').trigger('change');
        GetPrivilegedUsers();
    });
});

GetPrivilegedUsers();
$('#role_type').select2();

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