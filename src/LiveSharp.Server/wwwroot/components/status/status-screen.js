var statusScreen = Vue.component('status-screen', {
    template: `
    <section>
        <div class="flex flex-col h-full">
            <div class="w-full flex flex-wrap pb-4">
                <div class="flex-1 min-w-">
                    <h2>
                        <i v-bind:class="{ 'text-green-700': isServerStarted }" class="fas fa-desktop text-gray-500 w-10 pr-2"></i>
                        <span>Server</span>
                    </h2>            
                    <div class="md:pl-8">
                        <i class="fas fa-angle-right pr-2"></i>Server is {{serverStatus}}
                    </div>
                </div>
                <div class="flex-1">
                    <h2>
                        <i v-bind:class="{ 'text-green-700': isApplicationConnected }" class="fas fa-mobile-alt text-gray-500 w-10 pr-2"></i>
                        <span>Application</span>
                    </h2>                    
                    <div class="sm:pl-8">
                        <div><i class="fas fa-angle-right pr-2"></i>{{applicationStatus}}</div>
                        <div v-if="!!workspaceStatus"><i class="fas fa-angle-right pr-2"></i>{{workspaceStatus}}</div>
                        <div v-if="!!nuGetUpdateMessage"><i class="fas fa-angle-right pr-2"></i>{{nuGetUpdateMessage}}</div>
                    </div>
                </div>
            </div>
        </div>
    </section>
`,
    data() {
        return {            
            serverStatus: "not started",
            isServerStarted: false,
            isApplicationConnected: false,
            applicationStatus: "Waiting for application",
            workspaceStatus: "",
            availableNugetVersion: "",
            installedNugetVersion: "",
            nuGetUpdateMessage: ""            
        };
    },
    created() {
        this.server = new ScreenServer("StatusScreen", this);
    },
    methods: {
    }
});