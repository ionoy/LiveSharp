
var logScreen = Vue.component('log-screen', {
    template: `
    <section>
        <header class="section-header">
        </header>
        
        <div class="h-full">
            <log-container header="Server log" ref="logContainer" class="">
            </log-container>
            <log-container header="Application output" ref="applicationLogContainer" class="">
                If you don't see anything here even after the application has started, then check the following:<br/>

                1) LiveSharp NuGet package is installed in your main project<br/>
                2) Server status shows "Server is started"<br/>
                3) The Firewall doesn't have rules blocking ports 50540, 52540, 54540, 56540, 58540. You can temporarily disable the firewall to see if that helps<br/>
                4) Your development PC is accessible by TCP/IP from the running application. This usually means that both need to be located in the same network and have IP addresses assigned to the same subnetwork
            </log-container>    
        </div>
    </section>
`,
    data() {
        return {
        };
    },
    created() {
        this.server = new ScreenServer("LogScreen", this);
    },

    methods: {
        newApplicationLogText: function (text) {
            this.$refs.applicationLogContainer.newLogText(text);
        },
        newLogText: function (text) {
            this.$refs.logContainer.newLogText(text);
        }
    }
});