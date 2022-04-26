var menuItem = Vue.component('menu-item', {
    name: 'menu-item',
    props: ['title', 'nav', 'icon'],
    template: `
        <a v-on:click="selectScreen" v-bind:class="{ selected: isSelected }" class="nav-item text-center md:text-left cursor-pointer md:px-8">
            <i class="fa-fw text-gray-100 m-3 leading-loose md:ml-0 md:mr-3" v-bind:class="[iconSize, icon]"></i>
            <span class="hidden md:inline">{{title}}</span>
        </a>
`,
    data() {
        return {
            iconSize: "fas"
        };
    },
    methods: {
        selectScreen: function () {
            this.nav.selectedScreen = this.title;
        }
    },
    computed: {
        isSelected: function () {
            return this.nav.selectedScreen === this.title
        }
    }
});