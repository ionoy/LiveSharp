const { app, ipcRenderer, remote, clipboard } = require("electron");
const { machineId, machineIdSync } = require("node-machine-id");
const { VueAutosuggest } = require("vue-autosuggest");