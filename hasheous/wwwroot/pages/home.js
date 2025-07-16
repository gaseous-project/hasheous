// insights handling
fetch('/api/v1/Insights/app/0/Insights', {
    method: 'GET'
}).then(async function (response) {
    if (response.ok) {
        let insights = await response.json();

        let displayInsights = false;
        if (insights != null && Object.keys(insights).length > 0) {
            for (const [key, value] of Object.entries(insights)) {
                if (value == null || value == "") {
                    continue; // skip empty insights
                }

                displayInsights = true;

                // create insight element
                let insightElement = document.createElement('div');
                insightElement.classList.add('dataObjectInsight');

                let insightTitle = document.createElement('span');
                insightTitle.classList.add('insightHeading');
                insightTitle.innerHTML = lang.getLang(key);
                insightElement.appendChild(insightTitle);

                let insightContent = document.createElement('div');

                if (typeof value === 'object') {
                    // value is a hashtable, create a table with the keys and values
                    let insightList = document.createElement('table');
                    insightList.classList.add('tablerowhighlight');

                    for (const subKey of Object.keys(value)) {
                        let row = document.createElement('tr');
                        let keyCell = true;
                        for (const [subSubKey, subSubValue] of Object.entries(value[subKey])) {
                            let valueCell = document.createElement('td');
                            valueCell.classList.add('tablecell');
                            valueCell.style.width = '50%';
                            if (keyCell && subSubValue == null || subSubValue == "") {
                                valueCell.innerHTML = lang.getLang('unknown');
                            } else {
                                if (Number(subSubValue)) {
                                    valueCell.style.textAlign = 'right';
                                }
                                valueCell.innerHTML = subSubValue;
                            }
                            row.appendChild(valueCell);

                            keyCell = false;
                        }
                        insightList.appendChild(row);
                    }

                    insightContent.appendChild(insightList);
                } else {
                    if (value != null && value != "") {
                        displayInsights = true;

                        // otherwise, create a single row table with the value
                        let insightValue = document.createElement('table');

                        let valueRow = document.createElement('tr');
                        valueRow.classList.add('tablerowhighlight');

                        let valueCell = document.createElement('td');
                        valueCell.classList.add('tablecell');
                        if (Number(value)) {
                            valueCell.style.textAlign = 'right';
                        }
                        valueCell.innerHTML = value;
                        valueRow.appendChild(valueCell);
                        insightValue.appendChild(valueRow);

                        insightContent.appendChild(insightValue);
                    }
                }

                insightElement.appendChild(insightContent);
                insightElement.setAttribute('data-insight', key);

                document.getElementById('homepageinsightscontent').appendChild(insightElement);

            }
        }

        if (displayInsights) {
            document.getElementById('homepageinsights').style.display = '';
        }
    } else {
        throw new Error('Failed to fetch insights');
    }
});