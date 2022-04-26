
var inspectorMethodsTab = Vue.component('inspector-methods-tab', {
    template: `
    <div>
        <div v-if="isSearchVisible">
            <div>
                <span>Search</span>
                <input v-model="searchQuery" class="border border-black">
                <button v-on:click="closeSearch" class="text-red-600 text-sm hover:text-red-500">Cancel</button>
            </div>
                        
            <div class="mt-2">
                <div v-for="(method, index) in filteredMethods" v-on:click="addMethod(method)" class="text-sm hover:bg-gray-300 cursor-pointer" v-bind:class="{ 'bg-gray-200': index % 2 === 0 }">                    
                    <span class="">{{method.methodName}}</span> <span class="text-gray-500">{{method.containingTypeName}}</span>
                </div>
            </div>
        </div>
        <div class="mb-4" v-if="!isSearchVisible">
            <div class="text-sm text-center pt-2">
                <a v-on:click="isSearchVisible = true" class="hover:text-gray-500 underline" href="#">Add method</a>
            </div>
            <ul>
                <li v-for="addedMethod in addedMethods" class="border-b">
                    <div class="text-sm text-center overflow-hidden mt-1 mb-1">                    
                        <input type="checkbox" id="checkbox" checked class="float-left mt-1" v-on:click="togglePaused(addedMethod)" />
                        
                        <span class="float-left ml-1 text-gray-500">{{addedMethod.containingTypeName}}.</span>
                        <span class="float-left">{{addedMethod.methodName}}</span>
                        
                        <a class="text-red-500 hover:text-red-400 pr-1 float-right" v-on:click="removeMethod(addedMethod)" href="#"><i class="fas fa-times"></i></a>             
                    </div>
                    <div class="text-xs mb-2 overflow-y-auto" style="max-height: 24rem">
                        <div v-for="invocation in addedMethod.invocations">
                            <div v-on:click="toggleInvocationLog(invocation)" class="cursor-pointer hover:font-bold" v-bind:class="{ 'font-bold': invocation.logVisible }">
                                <span class="text-gray-600 pr-1">{{invocation.Time}}</span>
                                <span>{{addedMethod.methodName}}</span>
                                <span>({{invocation.Parameters}})</span>
                            </div>
                            <div class="bg-gray-700 text-gray-100">
                                <pre class="select-text p-1" v-if="invocation.logVisible" v-html="invocation.Log" />                                
                            </div>
                        </div>
                    </div>
                </li>
            </ul>
        </div>
    </div>
    `,
    data() {
        return {
            isSearchVisible: false,
            searchQuery: "",
            allInvocations: {},
            needToReplaceDebuggerValues: false,
            methods: [
                // TEST DATA
                // { ContainingTypeName: 'MyPage', MethodName: `CallMeHoney`, Id: 0,
                //     invocations: [
                //         { time: "12:08:10", info: '"abc", 1, MyModel', logVisible: false },
                //         { time: "12:08:18", info: '"a", 2, MyModel', logVisible: false },
                //         { time: "12:08:34", info: '"a asdf asdf bc", 6, MyModel', log: 'a\nbc d', logVisible: false },
                //     ]
                // },
                // { ContainingTypeName: 'MyViewModel', MethodName: `Jump`, Id: 1,
                //     invocations: [
                //         { time: "12:08:10", info: '"abc", 1, MyModel', logVisible: false },
                //         { time: "12:08:18", info: '"a", 2, MyModel', logVisible: false },
                //         { time: "12:08:34", info: '"a asdf asdf bc", 6, MyModel', logVisible: false },
                //     ] },
                // { ContainingTypeName: 'MyViewModel', MethodName: `AddToJumpingList`, Id: 2,
                //     invocations: [
                //         { time: "12:08:10", info: '"abc", 1, MyModel', logVisible: false },
                //         { time: "12:08:18", info: '"a", 2, MyModel', logVisible: false },
                //         { time: "12:08:34", info: '"a asdf asdf bc", 6, MyModel', logVisible: false },
                //     ] },
            ],
            addedMethods: [
            ]
        };
    },
    computed: {
        filteredMethods: function () {
            let query = this.searchQuery.toLowerCase();
            return this.methods.filter(function (method) {
                let methodName = method.methodName.toLowerCase();
                let containingTypeName = method.containingTypeName.toLowerCase();
                return methodName.indexOf(query) !== -1 || containingTypeName.indexOf(query) !== -1;
            })
        }
    },
    created() {
        this.server = new ScreenServer("MethodDebuggerScreen", this);
    },
    mounted: function() {
        var vm = this;
            
        window.setInterval(function() {
            if (vm.needToReplaceDebuggerValues)
                vm.replaceDebuggerValuesImpl();
        }, 100);
        document.body.onclick = function(e) {
            let target = e.target;
            if (target.className && target.className === "debugger-span") {
                navigator.clipboard.writeText(target.title);
            }
        }
    },
    methods: {
        addInvocation: function(invocationJson) {
            var invocation = JSON.parse(invocationJson);
            
            this.$set(invocation, 'logVisible', false);
            
            for (let method of this.addedMethods) {
                if (method.id === invocation.MethodId) { 
                    if (!method.invocations)
                        this.$set(method, 'invocations', []);
                    
                    method.invocations.push(invocation);
                    
                    if (method.invocations.length > 100) {
                        var toRemove = method.invocations[0];
                        method.invocations.splice(0, 1);
                        delete this.allInvocations[toRemove.Id];
                    }
                }
            }
            
            this.allInvocations[invocation.Id] = invocation;
        },
        completeInvocation: function(invocationId) {
            let invocation = this.allInvocations[invocationId];
            if (invocation) {
                invocation.IsCompleted = true;
            }
        },
        appendLog: function(invocationId, log) {
            let invocation = this.allInvocations[invocationId];
            if (invocation) {
                invocation.Log += log;
                this.replaceDebuggerValues();
            }
        },
        addMethod: function(method) {
            this.server.serverCall("watchMethod", method.id);
            this.addedMethods.push(method);
            this.isSearchVisible = false;
        },
        removeMethod: function(method) {
            this.server.serverCall("unwatchMethod", method.id);
            this.$delete(this.addedMethods, this.addedMethods.indexOf(method));
        },
        togglePaused: function(addedMethod) {
            addedMethod.isPaused = !addedMethod.isPaused;
            
            if (addedMethod.isPaused)
                this.server.serverCall("unwatchMethod", addedMethod.id);
            else
                this.server.serverCall("watchMethod", addedMethod.id);
        },
        closeSearch: function () {
            this.isSearchVisible = false;
        },
        toggleInvocationLog: function (invocation) {
            invocation.logVisible = !invocation.logVisible;
            this.replaceDebuggerValues();
        },
        replaceDebuggerValues: function() {
            this.$nextTick(function () {
                this.needToReplaceDebuggerValues = true;
            })
        },
        replaceDebuggerValuesImpl: function() {
            let valueElements = document.querySelectorAll(".debugger-span-value");
            for (let i = 0; i < valueElements.length; i++) {
                let valueElement = valueElements[i];
                let parentSpan = valueElement.parentElement;

                parentSpan.title = valueElement.innerText;
                parentSpan.className = "debugger-span";
                parentSpan.removeChild(valueElement);
            }
            this.needToReplaceDebuggerValues = false;            
        }
    }
});