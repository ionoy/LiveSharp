var logContainer = Vue.component('log-container', {
    props: ['header'],
    template: `
    <div class="bg-white shadow-md">
        <div class="text-white py-2 px-4 text-sm h-10 bg-gray-700">
            {{header}}
            <a class="float-right cursor-pointer" v-on:click="copyToClipboard">
                <i class="fas fa-copy"></i>
            </a>
        </div>
        <div readonly class="p-4 text-sm select-text overflow-y-auto">
            <p v-if="helpMessageVisible" class="text-xs text-gray-800">
                <slot></slot>
            </p>
            <div v-for="logItem in logItems" class="log-message"><pre>{{logItem}}</pre></div>
        </div>
    </div>
`,
    data() {
        return {
            helpMessageVisible: true,
            logItems: []
        };
    },
    created() {
        this.logContent = "";
    },
    methods: {
        newLogText: function (text) {
            this.helpMessageVisible = false;
            this.logItems.push(text);
            this.logContent += text + '\n';
        },
        copyToClipboard: function () {
            clipboard.writeText(this.logContent);
        }
    }
});