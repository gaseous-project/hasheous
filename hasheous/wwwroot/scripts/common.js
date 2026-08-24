async function ajaxCall(endpoint, method, successFunction, errorFunction, body) {
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
        headers: {
            'X-Hasheous-Web-Request': '1'
        },

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

async function postData(url, method, body, returnResult = false) {
    const token = await fetchAntiforgeryToken();
    const response = await fetch(url, {
        method: method,
        headers: {
            'Content-Type': 'application/json',
            'X-XSRF-TOKEN': token, // header name must match your backend config
            'X-Hasheous-Web-Request': '1'
        },
        credentials: 'include',
        body: JSON.stringify(body)
    });
    if (returnResult) {
        return response;
    }
    return response.json();
}

async function fetchAntiforgeryToken() {
    const response = await fetch('/api/v1.0/account/antiforgery-token', {
        headers: {
            'X-Hasheous-Web-Request': '1'
        },
        credentials: 'include' // ensures cookies are sent/received
    });
    const data = await response.json();
    return data.token;
}

if (typeof window !== 'undefined' && typeof window.fetch === 'function') {
    const originalFetch = window.fetch.bind(window);
    window.fetch = function (resource, options = {}) {
        const requestUrl = typeof resource === 'string'
            ? resource
            : (resource && typeof resource.url === 'string' ? resource.url : '');
        const isApiRequest = requestUrl.startsWith('/api/') || requestUrl.startsWith(window.location.origin + '/api/');

        if (isApiRequest) {
            const headers = new Headers(options.headers || (resource instanceof Request ? resource.headers : undefined) || {});
            headers.set('X-Hasheous-Web-Request', '1');
            options = { ...options, headers: headers };
        }

        return originalFetch(resource, options);
    };
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

/**
 * Checks whether a URL-like value is safe to use in HTML attributes such as href or src.
 * This blocks dangerous schemes that can execute script or load unsafe content.
 *
 * @param {string|undefined|null} value The candidate URL value.
 * @returns {boolean} True when the value is non-empty and does not use a blocked scheme.
 */
function isSafeUrl(value) {
    if (typeof value !== 'string') {
        return false;
    }

    const trimmedValue = value.trim();
    if (!trimmedValue) {
        return false;
    }

    const normalizedValue = trimmedValue.replace(/[\u0000-\u001F\u007F]/g, '');
    if (/^(javascript|vbscript|data|file):/i.test(normalizedValue)) {
        return false;
    }

    return true;
}

/**
 * Converts markdown into sanitized HTML for safe display in the UI.
 * The generated HTML is cleaned before insertion to prevent script execution,
 * inline event handlers, and unsafe URL schemes from surviving the render step.
 *
 * @param {string|undefined|null} markdownText The markdown content to render.
 * @returns {string} Sanitized HTML suitable for insertion into the page.
 */
function renderSafeMarkdown(markdownText) {
    const rawMarkdown = markdownText == null ? '' : String(markdownText);
    if (!rawMarkdown.trim()) {
        return '';
    }

    // marked.parse() is intentionally used here because the source content is markdown, not raw HTML.
    const parsedHtml = typeof marked !== 'undefined' && typeof marked.parse === 'function'
        ? marked.parse(rawMarkdown)
        : rawMarkdown;

    // Work on a detached DOM fragment so the original page is not modified while we clean it.
    const sanitizedRoot = document.createElement('div');
    sanitizedRoot.innerHTML = String(parsedHtml);

    // Remove active elements that can execute code or load unexpected content.
    sanitizedRoot.querySelectorAll('script, iframe, object, embed, svg, math, base, meta, link, style, template').forEach(node => node.remove());

    // Strip inline event handlers and blocked URL-bearing attributes from all nodes.
    sanitizedRoot.querySelectorAll('*').forEach(node => {
        Array.from(node.attributes).forEach(attribute => {
            const attributeName = attribute.name.toLowerCase();
            const attributeValue = (attribute.value || '').trim();

            if (attributeName.startsWith('on') || attributeName === 'srcdoc' || attributeName === 'style') {
                node.removeAttribute(attribute.name);
                return;
            }

            if (['href', 'src', 'xlink:href', 'action', 'formaction', 'background', 'poster'].includes(attributeName) && !isSafeUrl(attributeValue)) {
                node.removeAttribute(attribute.name);
            }
        });
    });

    return sanitizedRoot.innerHTML;
}

/**
 * Builds the URL of a data object's detail page.
 * @param {*} pageType The data object type (game, platform, company, app, ...)
 * @param {*} id The data object id
 * @returns The detail page URL
 */
function dataObjectDetailUrl(pageType, id) {
    return '/index.html?page=dataobjectdetail&type=' + encodeURIComponent(pageType) + '&id=' + encodeURIComponent(id);
}

/**
 * Generates an HTML table from a dataset.
 */
class generateTable {
    resultSet = undefined;

    table = undefined;

    /**
     * Generates an HTML table from a dataset.
     * @param {*} dataSet The dataset to generate the table from
     * @param {*} columns An array of column definitions - column name with an optional type separated by a colon (e.g. "name", "date:date", "size:bytes", "website:link", "description:lang", "logo:image")
     * @param {*} indexColumn The name of the column to use as the index (for row click callbacks)
     * @param {*} hideIndex Whether to hide the index column from display
     * @param {*} rowClickCallback A callback function to call when a row is clicked
     * @param {*} recordCount The total number of records in the dataset
     * @param {*} pageNumber The current page number
     * @param {*} pageCount The total number of pages
     * @param {*} pagingCallback A callback function to call when a page is changed
     * @param {*} rowLinkCallback A callback function returning the URL a row points at - rows built this way are real links, so they can be opened in a new tab, bookmarked, or copied
     * @returns 
     */
    constructor(dataSet, columns, indexColumn, hideIndex, rowClickCallback, recordCount, pageNumber, pageCount, pagingCallback, rowLinkCallback) {
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
                let cellName = '';
                if (columns[i].name) {
                    headerName = lang.getLang(columns[i].name);
                    cellName = columns[i].name;
                } else {
                    headerName = lang.getLang(columns[i].split(":")[0]);
                    let cellNameParts = columns[i].split(":");
                    cellName = cellNameParts[0];
                    if (cellNameParts[1]) {
                        switch (cellNameParts[1]) {
                            case "hideheading":
                                headerName = "";
                                break;
                        }
                    }
                }
                if (
                    (hideIndex === true && (headerName.toLowerCase() !== indexColumn.toLowerCase())) ||
                    (hideIndex === false)
                ) {
                    let headerCell = document.createElement('th');
                    headerCell.innerHTML = headerName;
                    headerCell.setAttribute('media-selector', 'cell_' + cellName.toLowerCase());
                    headerCell.classList.add('tableheadcell');
                    headerRow.appendChild(headerCell);
                }
            }
            genTable.appendChild(headerRow);

            for (let i = 0; i < this.resultSet.length; i++) {
                let dataRow = document.createElement('tr');
                let rowId = null;
                let rowHref = null;
                let rowCells = [];

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
                    let cellWidth = null;
                    switch (cellType) {
                        case "date":
                            if (cellValue.length > 0) {
                                cellContent.innerHTML = moment(cellValue + "Z").format('llll');
                            } else {
                                cellContent.innerHTML = "";
                            }
                            break;

                        case "lang":
                            cellContent.innerHTML = lang.getLang(cellValue);
                            break;

                        case "link":
                            if (cellValue.length > 0) {
                                // cellContent.innerHTML = "<a href=\"" + cellValue + "\" target=\"_blank\" rel=\"noopener noreferrer\">" + cellValue + "<img src=\"/images/link.svg\" class=\"linkicon\" /></a>";

                                let newLink = document.createElement('a');
                                newLink.href = cellValue;
                                newLink.target = "_blank";
                                newLink.rel = "noopener noreferrer";
                                newLink.innerHTML = cellValue + "<img src=\"/images/link.svg\" class=\"linkicon\" />";
                                newLink.addEventListener("click", function (ev) {
                                    window.open(cellValue, '_blank', 'noopener,noreferrer');
                                    ev.stopPropagation();
                                    ev.preventDefault();
                                }, true);
                                cellContent.appendChild(newLink);
                            } else {
                                cellContent.innerHTML = "";
                            }
                            break;

                        case "bytes":
                            if (Number(cellValue) > 0) {
                                cellContent.innerHTML = formatBytes(cellValue, 1);
                            } else {
                                cellContent.innerHTML = "";
                            }
                            break;

                        case "object":
                            cellContent = cellValue;
                            break;

                        case "image":
                            if (cellValue.length > 0) {
                                cellContent.innerHTML = "<div class=\"dataObjectLogoTable\"><img src=\"/api/v1/images/" + cellValue + "\" class=\"dataObjectLogoTableImg\" /></div>";
                            } else {
                                cellContent.innerHTML = "<div class=\"dataObjectLogoTable\"></div>";
                            }
                            cellWidth = "60px";
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
                        if (cellWidth != null) {
                            cell.style.width = cellWidth;
                        }
                        let cellName = '';
                        if (columns[x].name) {
                            cellName = columns[x].name;
                        } else {
                            cellName = columns[x].split(":")[0];
                        }
                        cell.setAttribute('media-selector', 'cell_' + cellName.toLowerCase());
                        rowCells.push({ cell: cell, content: cellContent });
                        dataRow.appendChild(cell);
                    }

                    if (cellName === indexColumn) {
                        dataRow.setAttribute('data-' + cellName, cellContent.innerHTML);
                        rowId = cellContent.innerHTML;
                    }
                }

                if (rowId != null && rowLinkCallback) {
                    rowHref = rowLinkCallback(rowId, this.resultSet);
                }

                for (let c = 0; c < rowCells.length; c++) {
                    // a cell that is already a link (the "link" column type) keeps its own
                    // anchor - nesting one inside the row link would be invalid markup
                    let cellHasLink = rowCells[c].content.tagName === 'A' ||
                        (rowCells[c].content.querySelector && rowCells[c].content.querySelector('a') != null);

                    if (rowHref && !cellHasLink) {
                        let cellLink = document.createElement('a');
                        cellLink.href = rowHref;
                        cellLink.classList.add('tablecelllink');
                        cellLink.appendChild(rowCells[c].content);
                        rowCells[c].cell.appendChild(cellLink);
                    } else {
                        rowCells[c].cell.appendChild(rowCells[c].content);
                    }
                }

                if (rowId != null) {
                    if (rowClickCallback) {
                        dataRow.classList.add('tablerowhighlight');
                        let clickbackResultSet = this.resultSet;
                        dataRow.addEventListener("click", function () {
                            rowClickCallback(rowId, clickbackResultSet);
                        }, false);
                    } else if (rowHref) {
                        dataRow.classList.add('tablerowhighlight');
                        dataRow.addEventListener("click", function (ev) {
                            // clicks on the anchors themselves are the browser's to handle, so
                            // modified clicks (new tab, new window, download) keep working
                            if (ev.target.closest('a')) {
                                return;
                            }
                            if (ev.button !== 0 || ev.metaKey || ev.ctrlKey || ev.shiftKey || ev.altKey) {
                                return;
                            }
                            window.location = rowHref;
                        }, false);
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

                if (pageCount > 5) {
                    // display a popup pager
                    let footerPager = document.createElement('div');
                    footerPager.classList.add('tablefooterpager');

                    // insert first page button
                    footerPager.appendChild(this.#createPageButton("|&lt;", 1, pageNumber, pageCount, pagingCallback));

                    // insert previous page button
                    footerPager.appendChild(this.#createPageButton("&lt;", pageNumber - 1, pageNumber, pageCount, pagingCallback));

                    // insert pager select
                    let pagerSelect = document.createElement('select');
                    for (let i = 1; i < pageCount + 1; i++) {
                        let pagerOption = document.createElement('option');
                        pagerOption.value = i;
                        pagerOption.innerHTML = i;
                        if (pageNumber == i) {
                            pagerOption.selected = true;
                        }
                        pagerSelect.appendChild(pagerOption);
                    }

                    pagerSelect.addEventListener("change", function () {
                        pagingCallback(pagerSelect.value);
                    })

                    footerPager.appendChild(pagerSelect);

                    // insert next page button
                    footerPager.appendChild(this.#createPageButton("&gt;", pageNumber + 1, pageNumber, pageCount, pagingCallback));

                    // insert last page button
                    footerPager.appendChild(this.#createPageButton("&gt;|", pageCount, pageNumber, pageCount, pagingCallback));

                    footer.appendChild(footerPager);
                } else if (pageCount > 1) {
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

        // Normalize object keys to lowercase to ensure case-insensitive property access
        if (value && typeof value === 'object') {
            let newValue = {};
            Object.keys(value).forEach(key => {
                newValue[key.toLowerCase()] = value[key];
            });
            value = newValue;
        }

        patternName = patternName.toLowerCase();

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

function hashCode(str) {
    var hash = 0;
    for (var i = 0; i < str.length; i++) {
        hash = str.charCodeAt(i) + ((hash << 5) - hash);
    }

    return hash;
}

function intToRGB(i) {
    var c = (i & 0x00FFFFFF)
        .toString(16)
        .toUpperCase();

    return "00000".substring(0, 6 - c.length) + c;
}

function getFlagEmoji(countryCode) {
    const codePoints = countryCode
        .toUpperCase()
        .split('')
        .map(char => 127397 + char.charCodeAt());
    return String.fromCodePoint(...codePoints);
}

let signatureSources = {
    0: "None",
    1: "TOSEC",
    2: "MAMEArcade",
    3: "MAMEMess",
    4: "NoIntros",
    5: "Redump",
    6: "WHDLoad",
    7: "RetroAchievements",
    8: "FBNeo",
    9: "PureDOSDAT",
    11: "MAMERedump",
    12: "TotalDOSCollection",
    13: "eXo",
    98: "ScreenScraper"
}

let tagTypes = {
    0: "GameGenre",
    1: "GameGameplay",
    2: "GameFeature",
    3: "GameTheme",
    4: "GamePerspective",
    5: "GameArtStyle",
    6: "PlatformType",
    7: "PlatformEra",
    8: "PlatformHardwareGeneration",
    9: "PlatformHardwareSpecs",
    10: "PlatformConnectivity",
    11: "PlatformInputMethod",
    1000: "Default"
}