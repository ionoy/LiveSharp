var newsScreen = Vue.component('news-screen', {
    template: `
    <section>
        <div class="flex flex-col h-full">
            <div id="newsPanel" class="external-html">
            
            </div>
        </div>
    </section>
`,
    mounted: function() {
        const newsFrame = document.getElementById('newsPanel');
        
        async function load_home() {
            let url = window.urls['news'];
            newsFrame.innerHTML = await (await fetch(url)).text();
        }
        
        const result = load_home();
    },
});