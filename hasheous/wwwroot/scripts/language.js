class language {
    locale = undefined;

    languageDefault = undefined;
    languageOverlay = undefined;

    langMapping =
        {
            "company":
            {
                "dataobject_objects": "companies",
                "dataobject_new": "newcompany"
            },
            "platform":
            {
                "dataobject_objects": "platforms",
                "dataobject_new": "newplatform"
            },
            "game":
            {
                "dataobject_objects": "games",
                "dataobject_new": "newgame"
            }
        };

    constructor() {

    }

    async Init(callback) {
        await this.InitAsync();
        console.log('Language files loaded');
        if (callback) {
            callback();
        }
    }

    async InitAsync() {
        // load base language
        if (getCookie("userLocale")) {
            this.locale = getCookie("userLocale");
        } else {
            this.locale = window.navigator.userLanguage || window.navigator.language;
        }
        console.log("Browser locale: " + this.locale);

        let language = this.locale.split("-")[0];
        let localisation = this.locale.split("-")[1];
        try {
            this.languageDefault = await $.getJSON('/localisation/' + language + '.json');
        } catch (e) {
            // something went wrong - default to en
            this.languageDefault = await $.getJSON('/localisation/en.json');
            console.warn("No suitable language file for " + language + ". Falling back to en");
            language = "en";
        }
        console.log("Loaded base language file: " + language + ".json");

        try {
            if (localisation) {
                // load overlay language
                this.languageOverlay = await $.getJSON('/localisation/' + language + '-' + localisation + '.json');
                console.log('Loaded language localisation file: ' + language + '-' + localisation + '.json');
            }
        } catch (e) {
            console.warn(e);
            this.languageOverlay = undefined;
        }

        this.applyLanguage();
    }

    // async getJSON(url) {
    //     return fetch(url)
    //         .then((response) => response.json())
    //         .then((responseJson) => { return responseJson });
    // }

    getLang(token, substituteArray) {
        let page = getQueryString('page', 'string');
        switch (page) {
            case "dataobjects":
            case "dataobjectnew":
                let pageType = getQueryString('type', 'string');

                let newToken = this.langMapping[pageType][token];
                if (newToken) {
                    token = newToken;
                }

                break;

            default:
                break;
        }

        if (this.languageOverlay) {
            if (token.toLowerCase() in this.languageOverlay) {
                return this.#replaceTokens(this.languageOverlay[token.toLowerCase()], substituteArray);
            }
        }

        if (this.languageDefault) {
            if (token.toLowerCase() in this.languageDefault) {
                return this.#replaceTokens(this.languageDefault[token.toLowerCase()], substituteArray);
            } else {
                return token.toLowerCase();
            }
        }
    }

    #replaceTokens(text, substituteArray) {
        let workText = text;
        if (substituteArray) {
            if (Array.isArray(substituteArray)) {
                // handle as an array
                for (let i = 0; i < substituteArray.length; i++) {
                    workText = workText.replace('{' + i + '}', substituteArray[i]);
                }
                return workText;
            } else {
                return text.replace('{0}', substituteArray);
            }
        } else {
            return text;
        }
    }

    setPageElementInnerHTMLLanguage(elementList) {
        for (let i = 0; i < elementList.length; i++) {
            let dataLang = $(elementList[i]).attr('data-lang');
            if (dataLang) {
                let dataLangStr = dataLang.replace(/\W/g, '');
                elementList[i].innerHTML = this.getLang(dataLangStr);
            }
        }
    }

    applyLanguage() {
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h1'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h2'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h3'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('span'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('p'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('div'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('th'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('td'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('label'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('a'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('button'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('option'));
    }
}