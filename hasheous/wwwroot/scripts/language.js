class language {
    locale = undefined;

    languageDefault = undefined;
    languageOverlay = undefined;

    constructor() {
        let loadComplete = false;
        this.Init(
            function() {
                loadComplete = true;
            }
        )
    }

    async Init(callback) {
        // load base language
        if (getCookie("userLocale")) {
            this.locale = getCookie("userLocale");
        } else {
            this.locale = window.navigator.userLanguage || window.navigator.language;
        }
        let language = this.locale.split("-")[0];
        let localisation = this.locale.split("-")[1];
        try {
            this.languageDefault = JSON.parse(await (await fetch('/localisation/' + language + '.json')).text());
        } catch (e) {
            // something went wrong - default to en
            this.languageDefault = JSON.parse(await (await fetch('/localisation/en.json')).text());
            console.warn("No suitable language file for " + language + ". Falling back to en");
            language = "en";
        }
        console.log("Loaded base language file: " + language + ".json");

        try {
            if (localisation) {
                // load overlay language
                this.languageOverlay = JSON.parse(await (await fetch('/localisation/' + language + '-' + localisation + '.json')).text());
                console.log('Loaded language localisation file: ' + language + '-' + localisation + '.json');
            }
        } catch(e) {
            console.warn(e);
            this.languageOverlay = undefined;
        }
    }

    getLang(token) {
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
                return this.languageOverlay[token.toLowerCase()];
            }
        }
        
        if (this.languageDefault) {
            if (token.toLowerCase() in this.languageDefault) {
                return this.languageDefault[token.toLowerCase()];
            } else {
                return token.toLowerCase();
            }
        }
    }
}