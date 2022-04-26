function ScreenServer(screenName, component) {
    this.screenName = screenName;

    this.serverCall = function (methodName) {
        var args = Array.prototype.slice.call(arguments, 1);
        ipcRenderer.send("server-call", {
            screenName: this.screenName,
            methodName: methodName,
            arguments: args
        });
    };

    ipcRenderer.on("renderer-call", (event, methodInfo) => {
        if (methodInfo.screenName === this.screenName) {
            var methodToCall = component[methodInfo.methodName];
            if (methodToCall) {
                methodToCall.apply(component, methodInfo.arguments);
            }
        }
    });

    ipcRenderer.on("sync-data", (event, dataInfo) => {
        if (dataInfo.screenName === this.screenName) {
            var data = dataInfo.data;
            for (let [key, value] of Object.entries(data)) {
                component[key] = value;
            }
        }
    });

    ipcRenderer.on("sync-data-value", (event, dataInfo) => {
        if (dataInfo.screenName === this.screenName) {
            component[dataInfo.name] = dataInfo.value;
        }
    });

    ipcRenderer.on("sync-list-add", (event, dataInfo) => {
        if (dataInfo.screenName === this.screenName) {            
            component[dataInfo.listName].push(dataInfo.item);
        }
    });

    ipcRenderer.on("sync-list-addRange", (event, dataInfo) => {
        if (dataInfo.screenName === this.screenName) {
            component[dataInfo.listName].push(...dataInfo.items);
        }
    });

    ipcRenderer.on("sync-list-clear", (event, dataInfo) => {
        if (dataInfo.screenName === this.screenName) {
            component[dataInfo.listName] = [];
        }
    });
    
    this.serverCall("ready");
}