function ajaxCall(endpoint, method, successFunction, errorFunction, body) {
    $.ajax({

        // Our sample url to make request
        url:
            endpoint,

        // Type of Request
        type: method,

        // data to send to the server
        data: body,

        dataType: 'json',
        contentType: 'application/json',

        // Function to call when to
        // request is ok
        success: function (data) {
            //let x = JSON.stringify(data);
            //console.log(x);
            successFunction(data);
        },

        // Error handling
        error: function (error) {
            if (errorFunction) {
                errorFunction(error);
            }
        }
    });
}

function getQueryString(stringName, type) {
    const urlParams = new URLSearchParams(window.location.search);
    let myParam = urlParams.get(stringName);

    switch (type) {
        case "int":
            if (typeof (Number(myParam)) == 'number') {
                return Number(myParam);
            } else {
                return null;
            }
        case "string":
            if (typeof (myParam) == 'string') {
                return encodeURIComponent(myParam);
            } else {
                return null;
            }
        default:
            return null;
    }
}

function setCookie(cname, cvalue, exdays) {
    const d = new Date();
    d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
    if (exdays) {
        let expires = "expires=" + d.toUTCString();
        document.cookie = cname + "=" + cvalue + ";" + expires + ";path=/";
    } else {
        document.cookie = cname + "=" + cvalue + ";path=/";
    }
}

function getCookie(cname) {
    let name = cname + "=";
    let decodedCookie = decodeURIComponent(document.cookie);
    let ca = decodedCookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

function formatBytes(bytes, decimals = 2) {
    if (!+bytes) return '0 Bytes'

    const k = 1024
    const dm = decimals < 0 ? 0 : decimals
    const sizes = ['Bytes', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB']

    const i = Math.floor(Math.log(bytes) / Math.log(k))

    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`
}

class generateTable {
    resultSet = undefined;

    table = undefined;

    constructor(dataSet, columns, indexColumn, hideIndex, rowClickCallback, recordCount, pageNumber, pageCount, pagingCallback) {
        this.resultSet = dataSet;

        if (hideIndex == undefined) {
            hideIndex = false;
        }

        if (this.resultSet.length == 0) {
            let errorMessage = document.createElement('span');
            errorMessage.innerHTML = lang.getLang('norecords');
            this.table = errorMessage;

            return this.table;
        } else {
            this.table = document.createElement('div');

            let genTable = document.createElement('table');

            // create header from attribute names in columns
            let headerRow = document.createElement('tr');
            if (!indexColumn) {
                indexColumn = "";
            }
            for (let i = 0; i < columns.length; i++) {
                let headerName;
                if (columns[i].name) {
                    headerName = lang.getLang(columns[i].name);
                } else {
                    headerName = lang.getLang(columns[i].split(":")[0]);
                }
                if (
                    (hideIndex === true && (headerName.toLowerCase() !== indexColumn.toLowerCase())) ||
                    (hideIndex === false)
                ) {
                    let headerCell = document.createElement('th');
                    headerCell.innerHTML = headerName;
                    headerCell.classList.add('tableheadcell');
                    headerRow.appendChild(headerCell);
                }
            }
            genTable.appendChild(headerRow);

            for (let i = 0; i < this.resultSet.length; i++) {
                let dataRow = document.createElement('tr');
                let rowId = null;

                for (let x = 0; x < columns.length; x++) {
                    let cellDetails;
                    if (columns[x].column) {
                        cellDetails = columns[x].column.split(":");
                    } else {
                        cellDetails = columns[x].split(":");
                    }
                    let cellName = cellDetails[0];
                    let cellType = '';
                    if (cellDetails[1]) {
                        cellType = cellDetails[1];
                    }

                    let rawCellValue = this.resultSet[i];

                    let cellValue = this.#processValue(cellName, rawCellValue, cellType);

                    let cellContent = document.createElement('span');
                    switch (cellType) {
                        case "date":
                            cellContent.innerHTML = moment(cellValue + "Z").format('llll');
                            break;

                        case "lang":
                            cellContent.innerHTML = lang.getLang(cellValue);
                            break;

                        case "link":
                            if (cellValue.length > 0) {
                                cellContent.innerHTML = "<a href=\"" + cellValue + "\" target=\"_blank\" rel=\"noopener noreferrer\">" + cellValue + "<img src=\"/images/link.svg\" class=\"linkicon\" /></a>";
                            } else {
                                cellContent.innerHTML = "";
                            }
                            break;

                        case "bytes":
                            cellContent.innerHTML = formatBytes(cellValue, 1);
                            break;

                        case "object":
                            cellContent = cellValue;
                            break;

                        default:
                            // default to plain text
                            cellContent.innerHTML = cellValue;
                            break;

                    }

                    if (
                        (hideIndex === true && (cellName.toLowerCase() !== indexColumn.toLowerCase())) ||
                        (hideIndex === false)
                    ) {
                        let cell = document.createElement('td');
                        cell.classList.add('tablecell');
                        cell.appendChild(cellContent);
                        dataRow.appendChild(cell);
                    }

                    if (cellName === indexColumn) {
                        dataRow.setAttribute('data-' + cellName, cellContent.innerHTML);
                        rowId = cellContent.innerHTML;
                    }
                }

                if (rowId != null) {
                    if (rowClickCallback) {
                        dataRow.classList.add('tablerowhighlight');
                        dataRow.addEventListener("click", function () {
                            rowClickCallback(rowId);
                        }, true);
                    }
                }

                genTable.appendChild(dataRow);
            }

            this.table.appendChild(genTable);

            // create table footer
            if (recordCount || pageCount) {
                let footer = document.createElement('div');
                footer.classList.add('tablefooter');

                if (recordCount) {
                    // display a label with the number of records returned
                    let footerRecordCount = document.createElement('div');
                    footerRecordCount.classList.add('tablefootercount');
                    let footerRecordCountLabel = document.createElement('span');
                    footerRecordCountLabel.innerHTML = lang.getLang('recordcount') + ' ' + recordCount;
                    footerRecordCount.appendChild(footerRecordCountLabel);

                    footer.appendChild(footerRecordCount);
                }

                if (pageCount > 1) {
                    // display a pager
                    let footerPager = document.createElement('div');
                    footerPager.classList.add('tablefooterpager');

                    // insert first page button
                    footerPager.appendChild(this.#createPageButton("|&lt;", 1, pageNumber, pageCount, pagingCallback));

                    // insert previous page button
                    footerPager.appendChild(this.#createPageButton("&lt;", pageNumber - 1, pageNumber, pageCount, pagingCallback));

                    for (let i = 0; i < pageCount; i++) {
                        let thisPage = (i + 1);

                        // insert page number button
                        footerPager.appendChild(this.#createPageButton(thisPage, thisPage, pageNumber, pageCount, pagingCallback));
                    }

                    // insert next page button
                    footerPager.appendChild(this.#createPageButton("&gt;", pageNumber + 1, pageNumber, pageCount, pagingCallback));

                    // insert last page button
                    footerPager.appendChild(this.#createPageButton("&gt;|", pageCount, pageNumber, pageCount, pagingCallback));

                    footer.appendChild(footerPager);
                }

                this.table.appendChild(footer);
            }

            return this.table;
        }
    }

    #processValue(pattern, value, type) {
        let patternName = pattern;
        let patternParts = pattern.split('.');

        let filter = null;
        if (patternParts[0].includes('[')) {
            patternName = patternParts[0].substring(0, patternParts[0].indexOf('['));

            let tempFilter = patternParts[0].substring(
                patternParts[0].indexOf("[") + 1,
                patternParts[0].lastIndexOf("]")
            );

            filter = {
                key: tempFilter.split('=')[0],
                value: tempFilter.split('=')[1]
            };
        } else {
            patternName = patternParts[0];
        }

        if (value[patternName]) {
            if (Array.isArray(value[patternName])) {
                let returnValue = '';
                for (let i = 0; i < value[patternName].length; i++) {
                    let obj = value[patternName][i];
                    if (filter != null) {
                        if (obj[filter.key]) {
                            if (obj[filter.key] == filter.value) {
                                // process this element
                                let tempPatternParts = patternParts;
                                tempPatternParts.shift();

                                returnValue = this.#processValue(
                                    tempPatternParts.join("."),
                                    obj,
                                    type
                                );
                            }
                        }
                    }
                }

                return returnValue;
            } else if (patternParts.length > 1) {
                let tempPatternParts = patternParts;
                tempPatternParts.shift();

                return (this.#processValue(
                    tempPatternParts.join("."),
                    value[patternName],
                    type
                ));
            } else {
                return value[patternName];
            }
        } else {
            return '';
        }
    }

    #createPageButton(displayText, targetPageNumber, currentPage, pageCount, callback) {
        let pageItem = document.createElement('span');
        pageItem.innerHTML = displayText;
        pageItem.classList.add('pageitem');

        if (
            targetPageNumber < 1 ||
            targetPageNumber == currentPage ||
            targetPageNumber > pageCount
        ) {
            pageItem.classList.add('selected');
        } else {
            pageItem.addEventListener("click", function (ev) {
                callback(targetPageNumber);
            });
        }

        return pageItem;
    }
}