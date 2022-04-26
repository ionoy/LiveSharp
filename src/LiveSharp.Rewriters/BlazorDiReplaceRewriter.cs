using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.Rewriters
{
    public class BlazorDiReplaceRewriter : RewriterBase
    {
        private readonly RuntimeMembers _runtimeMembers;
        private readonly ModuleDefinition _mainModule;
        private readonly RewriteLogger _logger;
        private readonly List<PropertyDefinition> _properties = new();
        private TypeReference _blazorServiceProvider;
        private MethodReference _blazorServiceProviderGetService;

        public BlazorDiReplaceRewriter(RuntimeMembers runtimeMembers, ModuleDefinition mainModule, RewriteLogger logger)
        {
            _runtimeMembers = runtimeMembers;
            _mainModule = mainModule;
            _logger = logger;
        }

        public override void ProcessSupportModule(ModuleDefinition module)
        {
            var blazorServiceProvider = module.GetAllTypes().FirstOrDefault(t => t.Name == "BlazorServiceProvider");

            if (blazorServiceProvider == null) {
                _logger.LogWarning("BlazorServiceProvider type not found");
                return;
            }
            
            var blazorServiceProviderGetService = blazorServiceProvider.Methods.FirstOrDefault(m => m.Name == "GetService");
            
            if (blazorServiceProviderGetService == null) {
                _logger.LogWarning("BlazorServiceProvider.Service method not found");
            }
            
            _blazorServiceProvider = _mainModule.ImportReference(blazorServiceProvider);
            _blazorServiceProviderGetService = _mainModule.ImportReference(blazorServiceProviderGetService);
        }

        public override void ProcessMainModuleType(TypeDefinition type)
        {
            if (type.Is("Microsoft.AspNetCore.Components.ComponentBase")) {
                var injectProperties = type
                    .Properties
                    .Where(p => p.CustomAttributes.Any(IsInjectAttribute) && !p.PropertyType.IsValueType);
                _properties.AddRange(injectProperties);
            }
        }

        public override void Rewrite()
        {
            foreach (var property in _properties)
                RewriteProperty(property);
        }

        private void RewriteProperty(PropertyDefinition property)
        {
            if (_blazorServiceProvider == null || _blazorServiceProviderGetService == null)
                return;
            
            if (!property.GetMethod.HasBody) {
                _logger.LogWarning($"Can't find method body for {property.FullName} getter");
                return;
            }

            var methodBody = property.GetMethod.Body;
            var ilProcessor = methodBody.GetILProcessor();
            var instructions = methodBody.Instructions;
            var ret = instructions.FirstOrDefault(i => i.OpCode == OpCodes.Ret);

            if (ret == null) {
                _logger.LogWarning($"Missing `ret` in {property.FullName} getter");
                return;
            }
         
            var backingFieldName = GetBackingFieldName(property.Name);
            var backingField = property.DeclaringType.Fields.FirstOrDefault(f => f.Name == backingFieldName);
            
            if (backingField == null) {
                _logger.LogWarning($"Missing backing field for {property.FullName}");
                return;
            }
            
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Dup));
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ldnull));
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ceq));
            // if not null, jump to ret
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Brfalse, ret));
            
            // otherwise call BlazorServiceProvider.GetService(property.PropertyType)
            // pop field value from previous dup
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Pop));
            
            // load `this` for stfld
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ldarg_0));
            
            // get property type
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ldtoken, property.PropertyType));
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Call, _runtimeMembers.GetTypeFromHandleMethod));
            
            // call GetService
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Call, _blazorServiceProviderGetService));
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Castclass, property.PropertyType));
            
            // store service to backing field
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Stfld, backingField));
            
            // load it for return
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ldarg_0));
            ilProcessor.InsertBefore(ret, Instruction.Create(OpCodes.Ldfld, backingField));

            // Remove Inject attribute so Blazor DI wouldn't try to inject non-existent service
            var injectAttribute = property.CustomAttributes.FirstOrDefault(IsInjectAttribute);
            property.CustomAttributes.Remove(injectAttribute);
        }

        private static bool IsInjectAttribute(CustomAttribute a)
        {
            return a.AttributeType.FullName == "Microsoft.AspNetCore.Components.InjectAttribute";
        }
        
        private string GetBackingFieldName(string propertyName) => $"<{propertyName}>k__BackingField";
    }
}