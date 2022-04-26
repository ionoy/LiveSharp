using Mono.Cecil;
using System;
using System.Linq;

namespace LiveSharp.Rewriters
{
    static internal class AttributeLoader
    {
        public static void LoadBuildAttributes(ModuleDefinition module, LiveSharpStartRewriter startRewriter, UpdateHookRewriter updateHookRewriter)
        {
            var liveSharpAttributes = module.GetCustomAttributes().Where(a => a.AttributeType.FullName.StartsWith("LiveSharp."))
                .ToArray();

            if (startRewriter != null)
                LoadStartAttributes(startRewriter, liveSharpAttributes);

            LoadUpdateHookAttributes(updateHookRewriter, liveSharpAttributes);
            LoadSkipStartAttributes(startRewriter, liveSharpAttributes);
        }
        
        private static void LoadSkipStartAttributes(LiveSharpStartRewriter startRewriter, CustomAttribute[] liveSharpAttributes)
        {
            startRewriter.SkipStart = liveSharpAttributes.Any(a => a.AttributeType.Name == "LiveSharpSkipStartAttribute");
        }

        private static void LoadUpdateHookAttributes(UpdateHookRewriter updateHookRewriter, CustomAttribute[] liveSharpAttributes)
        {
            foreach (var injectAttribute in liveSharpAttributes.Where(a => a.AttributeType.Name == "LiveSharpInjectAttribute"))
                updateHookRewriter.InjectRules.Add(CreateInjectRule(injectAttribute));

            foreach (var excludeAttribute in liveSharpAttributes.Where(a => a.AttributeType.Name == "LiveSharpExcludeAttribute"))
                updateHookRewriter.InjectExcludeRules.Add(CreateInjectRule(excludeAttribute));

            if (updateHookRewriter.InjectRules.Count == 0) {
                updateHookRewriter.InjectRules.Add(new InjectRule("*"));
            }
        }

        private static void LoadStartAttributes(LiveSharpStartRewriter startRewriter, CustomAttribute[] liveSharpAttributes)
        {
            foreach (var startAttribute in liveSharpAttributes.Where(a => a.AttributeType.Name == "LiveSharpStartAttribute")) {
                startRewriter.StartRule = CreateInjectRule(startAttribute);
            }

            var ipAttribute = liveSharpAttributes.FirstOrDefault(a => a.AttributeType.Name == "LiveSharpServerIpAttribute");
            if (ipAttribute != null) {
                startRewriter.ServerIp = ipAttribute.ConstructorArguments[0].Value?.ToString();
            }
        }

        private static InjectRule CreateInjectRule(CustomAttribute injectAttribute)
        {
            var args = injectAttribute.ConstructorArguments;

            if (args.Count == 1) {
                if (args[0].Type.FullName == "System.String") {
                    var pattern = args[0].Value.ToString();
                    return new InjectRule(pattern);
                }

                if (args[0].Type.FullName == "System.Type") {
                    var type = (TypeReference)args[0].Value;
                    return new InjectRule(type.FullName);
                }
            }
            else if (args.Count == 2) {
                var type = (TypeReference)args[0].Value;
                var methodName = (string)args[1].Value;

                // Handle case where developer put type name as ctor name
                //[LiveSharpStart(typeof(App), nameof(App)]
                if (type.Name == methodName)
                    methodName = ".ctor";
                
                return new InjectRule(type.FullName, methodName);
            }
            else if (args.Count > 2) {
                var type = (TypeReference)args[0].Value;
                var methodName = (string)args[1].Value;
                var parameterTypes = ((CustomAttributeArgument[])args[2].Value).Select(a => ((TypeReference)a.Value).FullName);

                return new InjectRule(type.FullName, methodName, parameterTypes.ToArray());
            }

            throw new InvalidOperationException("Invalid LiveSharpInject/LiveSharpExclude attribute usage");
        }
    }
}