function GetPrivilegedUsers() {
    ajaxCall(
        '/api/v1/AccountAdmin/Users',
        'GET',
        function (success) {
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
        },
        function (error) {

        }
    );
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

    ajaxCall(
        '/api/v1/AccountAdmin/Users/' + email + '/Roles?RoleName=' + role,
        'POST',
        function (success) {
            document.getElementById('role_email').value = '';
            $('#role_type').val('None').trigger('change');
            GetPrivilegedUsers();
        },
        function (error) {
            document.getElementById('role_email').value = '';
            $('#role_type').val('None').trigger('change');
            GetPrivilegedUsers();
        }
    );
});

GetPrivilegedUsers();
$('#role_type').select2();