function LoadStatusPage() {
    const statusTable = document.getElementById('status-table');

    let columnCount = 5; // Default column count

    fetch('/api/v1.0/BackgroundTasks', {
        method: 'GET'
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            taskList = data; // Store the fetched data for potential future use
            let tableContent = document.createElement('table');
            for (const server of Object.keys(data)) {
                if (data[server] && data[server].length > 0) {
                    // build the table

                    let serverData = data[server];

                    // Add server header
                    const serverHeader = document.createElement('tr');
                    serverHeader.innerHTML = `<td colspan="${columnCount}"><h2>${lang.getLang('servicebackgroundtasksfor' + server)}</h2></td>`;
                    tableContent.appendChild(serverHeader);

                    const headRow = document.createElement('tr');

                    const headCell1 = document.createElement('th');
                    headCell1.className = 'tablecell';
                    headCell1.textContent = lang.getLang('serviceitemtype');
                    headRow.appendChild(headCell1);

                    const headCell2 = document.createElement('th');
                    headCell2.className = 'tablecell';
                    headCell2.textContent = lang.getLang('serviceitemstate');
                    headRow.appendChild(headCell2);

                    const headCell3 = document.createElement('th');
                    headCell3.className = 'tablecell';
                    headCell3.textContent = lang.getLang('serviceitemlastruntime');
                    headRow.appendChild(headCell3);

                    const headCell4 = document.createElement('th');
                    headCell4.className = 'tablecell';
                    headCell4.textContent = lang.getLang('serviceitemnextruntime');
                    headRow.appendChild(headCell4);

                    const headCell5 = document.createElement('th');
                    headCell5.className = 'tablecell';
                    headCell5.innerHTML = '&nbsp;'; // Empty cell for buttons
                    headRow.appendChild(headCell5);

                    tableContent.appendChild(headRow);

                    const tableGroups = {
                        "signatures": [
                            "SignatureIngestor",
                            "FetchTOSECMetadata",
                            "FetchRedumpMetadata",
                            "FetchMAMERedumpMetadata",
                            "FetchWHDLoadMetadata",
                            "FetchFBNEOMetadata",
                            "FetchPureDOSDATMetadata"
                        ],
                        "metadataproxy": [
                            "FetchIGDBMetadata",
                            "FetchTheGamesDbMetadata",
                            "FetchVIMMMetadata",
                            "FetchGiantBombMetadata",
                            "FetchRetroAchievementsMetadata"
                        ],
                        "maintenance": [
                            "DailyMaintenance",
                            "WeeklyMaintenance",
                            "CacheWarmer"
                        ],
                        "servicemanagement": [
                            "TallyVotes",
                            "MetadataMatchSearch",
                            "GetMissingArtwork",
                            "MetadataMapDump"
                        ]
                    }

                    // Group tasks by type
                    Object.keys(tableGroups).forEach(group => {
                        const groupItems = tableGroups[group];

                        const groupBody = document.createElement('tbody');
                        const groupHeader = document.createElement('tr');
                        groupHeader.innerHTML = `<td colspan="${columnCount}"><h3>${lang.getLang('service' + group)}</h3></td>`;
                        groupBody.appendChild(groupHeader);

                        let groupFound = false;
                        tableGroups[group].forEach(itemType => {
                            const task = serverData.find(task => task.itemType === itemType);
                            if (task) {
                                let itemState = task.itemState;
                                if (task.enabled === false) {
                                    itemState = 'Disabled';
                                }

                                const row = document.createElement('tr');
                                row.setAttribute('data-processid', task.processId || '');

                                const rowCell1 = document.createElement('td');
                                rowCell1.className = 'tablecell';
                                rowCell1.textContent = lang.getLang('service' + task.itemType);
                                row.appendChild(rowCell1);

                                const rowCell2 = document.createElement('td');
                                rowCell2.className = 'tablecell';
                                rowCell2.textContent = lang.getLang('service' + itemState);
                                row.appendChild(rowCell2);

                                const rowCell3 = document.createElement('td');
                                rowCell3.className = 'tablecell';
                                rowCell3.textContent = task.lastRunTime ? new Date(task.lastRunTime).toLocaleString() : '-';
                                row.appendChild(rowCell3);

                                const rowCell4 = document.createElement('td');
                                rowCell4.className = 'tablecell';
                                rowCell4.textContent = task.nextRunTime ? new Date(task.nextRunTime).toLocaleString() : '-';
                                row.appendChild(rowCell4);

                                const rowCell5 = document.createElement('td');
                                rowCell5.className = 'tablecell';
                                rowCell5.style.textAlign = 'right';
                                row.appendChild(rowCell5);

                                if (userProfile != null) {
                                    if (userProfile.Roles != null) {
                                        let startButton = '';
                                        if (userProfile.Roles.includes('Admin') && ['Stopped', 'NeverStarted'].includes(itemState)) {
                                            startButton = `<button class="btn btn-primary" onclick="SetTask('${server}', '${task.itemType}', '${task.processId}', true);">${lang.getLang('servicestart')}</button>`;
                                        }

                                        let enabledButtonLabel = task.enabled ? lang.getLang('disable') : lang.getLang('enable');
                                        let enabledToggle = `<button class="btn btn-primary" onclick="SetTask('${server}', '${task.itemType}', '${task.processId}', null, ${!task.enabled});">${enabledButtonLabel}</button>`;

                                        rowCell5.innerHTML += startButton;
                                        rowCell5.innerHTML += enabledToggle;
                                    }
                                }

                                groupBody.appendChild(row);
                                groupFound = true;
                            }
                        });

                        if (groupFound) {
                            tableContent.appendChild(groupBody);
                        }
                    });
                } else {
                    tableContent.innerHTML = `<tr><td colspan="${columnCount}">No background tasks found.</td></tr>`;
                }
            }

            statusTable.innerHTML = tableContent.innerHTML;
        })
        .catch(error => {
            console.error('Error fetching background tasks:', error);
            statusTable.innerHTML = `<tr><td colspan="${columnCount}">Error loading status. Please try again later.</td></tr>`;
        });
}

let taskList = {};

LoadStatusPage();

let statusRefresh = setInterval(() => {
    LoadStatusPage();
}, 5000); // Refresh every 5 seconds

window.addEventListener('beforeunload', () => {
    clearInterval(statusRefresh);
});

function SetTask(server, itemType, processId, forceRun = null, enabled = null) {
    let args = "";
    if (forceRun !== null) {
        args += `ForceRun=${forceRun}`;
    }
    if (enabled !== null) {
        args += `Enabled=${enabled}`;
    }

    if (arguments.length > 0) {
        fetch(`/api/v1.0/BackgroundTasks/${processId}?${args}`, {
            method: 'GET'
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                LoadStatusPage(); // Reload the status page after starting the task
                return response.json();
            })
            .catch(error => {
                console.error('Error starting task:', error);
                alert(lang.getLang('servicestarterror') + ': ' + error.message);
            });
    }
}
