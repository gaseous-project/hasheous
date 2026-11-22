class language {
    locale = undefined;
    selectedLocale = undefined;

    languages = undefined;

    languageDefault = undefined;
    languageBase = undefined;
    languageRegion = undefined;

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
            },
            "app":
            {
                "dataobject_objects": "apps",
                "dataobject_new": "newapp"
            },
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

        // load language list from the server (languages.json)
        try {
            this.languages = await $.getJSON('/localisation/languages.json');
        } catch (e) {
            console.error("Failed to load languages list:", e);
        }

        // loading default language file - this is always en as it's always the most complete
        try {
            let langDefault = await $.getJSON('/localisation/en.json');
            this.languageDefault = this.#ExtractStrings(langDefault);
            this.selectedLocale = "en";
            console.log('Loaded default language file: en.json');
        } catch (e) {
            console.error("Failed to load default language file (en.json):", e);
        }

        // load language files based on the locale
        let language = this.locale.split("-")[0];
        let localisation = this.locale.split("-")[1];

        // load the base language file
        try {
            let langBase = await $.getJSON('/localisation/' + language + '.json');
            this.languageBase = this.#ExtractStrings(langBase);
            this.selectedLocale = language;
            console.log('Loaded base language file: ' + language + '.json');
        } catch (e) {
            console.warn("Failed to load base language file for " + language + ".");
        }

        // load the regional language file
        if (localisation) {
            try {
                let langRegion = await $.getJSON('/localisation/' + this.locale + '.json');
                if (langRegion?.translations) {
                    this.languageRegion = this.#ExtractStrings(langRegion);
                    this.selectedLocale = this.locale;
                    console.log('Loaded language localisation file: ' + this.locale + '.json');
                }
            } catch (e) {
                console.warn("Failed to load regional language file for " + this.locale + ".");
            }
        }

        if (!this.languageBase && !this.languageRegion) {
            console.warn("No language files loaded for locale: " + this.locale + ". Falling back to default language (en).");
        }

        if ((!this.languageBase || (!this.languageRegion && localisation)) && this.locale != "en") {
            // place request for language matching the locale
            try {
                await $.getJSON('/localisation/request/' + this.locale + '.json');
            } catch (e) {
                console.warn("Failed to request language file for locale: " + this.locale + ".", e);
            }
        }

        console.log(this.languageRegion);
        console.log(this.languageBase);
        console.log(this.languageDefault);

        console.log("Selected language: " + this.selectedLocale);

        document.documentElement.lang = this.selectedLocale;

        this.applyLanguage();
    }

    #ExtractStrings(language) {
        let strings = {};
        if (language?.translations) {
            for (let key in language.translations) {
                if (language.translations.hasOwnProperty(key)) {
                    strings[key.toLowerCase()] = language.translations[key].value;
                }
            }
        }
        return strings;
    }

    getLang(token, substituteArray, bypassMarkdown = false) {
        // check if token is null or undefined
        if (token === null || token === undefined) {
            return '';
        }

        // check if token is not a string
        if (typeof token !== 'string') {
            return token;
        }

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

        // store the lowercase version of the token as the default return value
        let returnValue = token.toLowerCase();
        let tokenFound = false;

        // check if token exists in the regional language first
        if (this.languageRegion) {
            if (token.toLowerCase() in this.languageRegion) {
                returnValue = this.#replaceTokens(this.languageRegion[token.toLowerCase()], substituteArray);
                tokenFound = true;
            }
        }

        // check if token exists in the base language next
        if (this.languageBase && !tokenFound) {
            if (token.toLowerCase() in this.languageBase) {
                returnValue = this.#replaceTokens(this.languageBase[token.toLowerCase()], substituteArray);
                tokenFound = true;
            }
        }

        // if token not found in either language region or base, check default language
        if (this.languageDefault && !tokenFound) {
            if (token.toLowerCase() in this.languageDefault) {
                returnValue = this.#replaceTokens(this.languageDefault[token.toLowerCase()], substituteArray);
                tokenFound = true;
            }
        }

        if (!bypassMarkdown) {
            returnValue = renderSafeMarkdown(returnValue);
        }

        // if token not found in either language region or base, return the token itself
        return returnValue;
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