
var appScreen = Vue.component('app-screen', {
    template: `
<div>
    <nav class="bg-gray-700 float-left w-16 md:w-64 h-screen">
        <header class="hidden text-gray-100 md:p-8 md:block">
            <h1>LiveSharp Server</h1>
            <small>{{currentVersion}}</small>
        </header> 

        <div class="text-gray-100">
            <menu-item title="Status" :nav="nav" icon="fa-heart"></menu-item>
            <menu-item title="News" :nav="nav" icon="fa-newspaper"></menu-item>
            <menu-item title="Inspector" :nav="nav" icon="fa-glasses"></menu-item>
            <menu-item title="Log" :nav="nav" icon="fa-align-justify"></menu-item>
            <menu-item title="License" :nav="nav" icon="fa-key"></menu-item>
            <menu-item title="Help" :nav="nav" icon="fa-book"></menu-item>
        </div>
        
        <footer>
            <div class="hidden md:block md:p-8">{{updateInformation}}</div>
            <label class="block text-xs text-center">
              <input v-model="alwaysOnTop" class="text-xl" type="checkbox">
              <span class="hidden md:block text-xs">
                Always on top
              </span>
            </label>
        </footer>
    </nav>

    <div class="ml-16 md:ml-64 overflow-y-auto h-screen">
        <div class="p-4 bg-gray-100 text-gray-900 h-full" >
            <status-screen v-show="nav.selectedScreen == 'Status'"></status-screen>
            <news-screen v-show="nav.selectedScreen == 'News'"></news-screen>
            <log-screen v-show="nav.selectedScreen == 'Log'"></log-screen>
            <license-screen v-show="nav.selectedScreen == 'License'"></license-screen>
            <inspector-screen v-show="nav.selectedScreen == 'Inspector'"></inspector-screen>
            <help-screen v-show="nav.selectedScreen == 'Help'"></help-screen>
        </div>
    </div>
</div>
`,
    data() {
        return {
            nav: {
                selectedScreen: "Status"
            },
            alwaysOnTop: false,
            updateInformation: "",
            currentVersion: "0.0.1"
        };
    },
    created() {
        this.server = new ScreenServer("AppScreen", this);

    },
    methods: {
        updateAvailable: function (information) {
            this.updateInformation = information;
        }
    },
    watch: {
        alwaysOnTop: function(val) {
            this.server.serverCall("alwaysOnTop", val);
        }
    }
});