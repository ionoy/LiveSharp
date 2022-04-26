var licenseScreen = Vue.component('license-screen', {
    template: `
    <section>
        <header>
        </header>
        
        <div class="w-full max-w-xs mx-auto">
          <form class="bg-white shadow-md rounded px-8 pt-6 pb-8 mb-4">
            <p class="pb-4">
                {{licenseMessage}}
                <i v-if="licenseIsValid" class="fas fa-check text-green-800"></i>
            </p>
            <div class="mb-4">
              <label class="block text-gray-700 text-sm font-bold mb-2" for="username">
                Subscription email
              </label>
              <input v-model="email" class="shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 leading-tight focus:outline-none focus:shadow-outline" id="username" type="text" placeholder="Username">
            </div>
            <div class="mb-6">
              <label class="block text-gray-700 text-sm font-bold mb-2" for="password">
                Password
              </label>
              <input v-model="password" class="shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 mb-3 leading-tight focus:outline-none focus:shadow-outline" id="password" type="password" placeholder="******************">
            </div>
            <p class="text-center text-gray-500 text-sm pb-4">
                You can use your existing LiveXAML license
            </p>
            <div class="items-center">
              <button v-on:click="loadLicense" class="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded focus:outline-none focus:shadow-outline" type="button">
                Load license
              </button>
            </div>
          </form>
        </div>
        <div class="text-center">
            <a href="https://www.livesharp.net/" class="">Purchase a license</a>
        </div>
    </section>
`,
    data() {
        return {
            email: "",
            password: "",
            licenseMessage: "",
            licenseIsValid: false,
            expiration: ""
        };
    },
    created() {
        this.server = new ScreenServer("LicenseScreen", this);

        machineId().then((id) => {
            this.server.serverCall("set-machine-id", id);
        });
    },

    methods: {
        loadLicense: function () {
            this.server.serverCall("load-license", this.email, this.password);
        }
    }
});