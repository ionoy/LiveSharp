
var inspectorScreen = Vue.component('inspector-screen', {
    template: `
    <section class="border shadow-lg overflow-y-auto">
        <ul class="list-reset flex font-semibold border-b-2">
          <li class="-mx-px">
            <a v-on:click="selectTab('debugger')"   class="inline-block py-2 px-4 text-gray-500" v-bind:class="{ selected_tab: selectedTab == 'debugger' }" href="#">Debugger</a>
          </li>
          <li class="-mx-px">
            <a v-on:click="selectTab('viewmodels')" class="inline-block py-2 px-4 text-gray-500" v-bind:class="{ selected_tab: selectedTab == 'viewmodels' }" href="#">ViewModels</a>
          </li>
        </ul>
        <div v-if="isApplicationConnected" class="p-2 flex flex-col h-full">
            <div id="tab_viewmodels" v-if="selectedTab == 'viewmodels'" class="mb-2">
                <div v-if="instances.length == 0" class="w-full flex flex-wrap text-sm">
                    <p>No ViewModels detected</p>
                </div>
                <div v-if="instances.length > 0" class="w-full flex flex-wrap">
                    <div v-for="instance in instances" class="max-w-sm bg-gray-700 rounded-sm shadow-xl select-text mt-2 mb-2 mr-2">
                        <div class="text-white text-sm p-1 sm:py-2 sm:px-4">
                            <i class="fas fa-cubes pr-1"></i>
                            {{instance.typeName}}
                        </div>
                        <div class="bg-white text-xs sm:p-4 overflow-auto">
                            <table>
                                <tr v-for="property in instance.properties">
                                    <td class="pr-2 text-right font-bold">{{property.name}}</td>
                                    <td>{{property.value}} </td>
                                </tr>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
            <inspector-methods-tab ref="methodsTab" v-if="selectedTab == 'debugger'" id="tab_debugger"></inspector-methods-tab>
        </div>
        <div v-if="!isApplicationConnected" class="p-2 flex flex-wrap">
            <i class="fas fa-hourglass-half mt-12 pl-2 text-5xl text-center w-full"></i>
            <h2 class="text-center w-full mt-6">Waiting for application to connect</h2>            
        </div>
    </section>
`,
    data() {
        return {
            selectedTab: "debugger",
            isApplicationConnected: true,
            instances: [
                // TEST DATA
                // {
                //     typeName: "App1.ViewModels.MainViewModel",
                //     properties: [
                //         { name: "Title", value: "Main" },
                //         { name: "User", value: "Mihhail" },
                //         { name: "Count", value: "25" },
                //         { name: "IsAlive", value: "true" },
                //     ]
                // },
                // {
                //     typeName: "App1.ViewModels.MainViewModel",
                //     properties: [
                //         { name: "Title", value: "Main" },
                //         { name: "User", value: "Mihhail" },
                //         { name: "Count", value: "25" },
                //         { name: "IsAlive", value: "true" },
                //     ]
                // },
                // {
                //     typeName: "App1.ViewModels.MainViewModel",
                //     properties: [
                //         { name: "Title", value: "Main" },
                //         { name: "User", value: "Mihhail" },
                //         { name: "Count", value: "25" },
                //         { name: "IsAlive", value: "true" },
                //     ]
                // }
            ]
        };
    },
    created() {
        this.server = new ScreenServer("InspectorScreen", this);
    },
    methods: {
        updateInstances: function(jsonText) {
            var jsonObject = JSON.parse(jsonText);
            this.instances = jsonObject.instances;
        },
        selectTab: function(tabName) {
            this.selectedTab = tabName;
        }
    }
});