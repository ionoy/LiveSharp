var helpScreen = Vue.component('help-screen', {
    template: `
    <section>
        <div class="flex flex-col h-full">
            <div id="helpPanel" class="external-html">
            
            </div>
        </div>
    </section>
`,
    mounted: function() {
        const newsFrame = document.getElementById('helpPanel');
        
        async function load_home() {
            let url = window.urls['help'];
            newsFrame.innerHTML = await (await fetch(url)).text();
        }
        
        const result = load_home();
    },
});