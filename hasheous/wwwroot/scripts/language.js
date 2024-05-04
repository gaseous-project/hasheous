class language {
    locale = undefined;

    languageDefault = undefined;
    languageOverlay = undefined;

    constructor() {
        let loadComplete = false;
        // this.Init(
        //     function () {
        //         loadComplete = true;
        //         console.log("Poo");
        //         callback();
        //     }
        // )
        // console.log("fuck");

        Promise.resolve(this.Init()).then(console.log('Language files loaded'));
    }

    async Init() {
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
            this.languageDefault = await this.getJSON('/localisation/' + language + '.json');
        } catch (e) {
            // something went wrong - default to en
            this.languageDefault = await this.getJSON('/localisation/en.json');
            console.warn("No suitable language file for " + language + ". Falling back to en");
            language = "en";
        }
        console.log("Loaded base language file: " + language + ".json");

        try {
            if (localisation) {
                // load overlay language
                this.languageOverlay = await this.getJSON('/localisation/' + language + '-' + localisation + '.json');
                console.log('Loaded language localisation file: ' + language + '-' + localisation + '.json');
            }
        } catch (e) {
            console.warn(e);
            this.languageOverlay = undefined;
        }
    }

    async getJSON(url) {
        return fetch(url)
            .then((response) => response.json())
            .then((responseJson) => { return responseJson });
    }

    getLang(token, substituteArray) {
        let page = getQueryString('page', 'string');
        switch (page) {
            case "dataobjects":
            case "dataobjectnew":
                let pageType = getQueryString('type', 'string');

                let langMapping =
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

                let newToken = langMapping[pageType][token];
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
}