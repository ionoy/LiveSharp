using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiveSharp.CSharp;
using LiveSharp.Rewriters;
using LiveSharp.Rewriters.Serialization;
using Xamarin.Forms;
using LiveSharp.Runtime;
using LiveSharp.RuntimeTests.Tests;
using LiveSharp.Runtime.IL;
using LiveSharp.RuntimeTests.Infrastructure;
using LiveSharp.RuntimeTests.Sequences;
using LiveSharp.RuntimeTests.Sequences.ChangeReturnValue;
using LiveSharp.RuntimeTests.Sequences.DynamicIndex;
using LiveSharp.Shared.Api;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using XamarinWorkbench;
using LiveSharpHandler = LiveSharp.CSharp.LiveSharpHandler;

namespace LiveSharp.RuntimeTests
{
    public class MyClass<T>
    {
        
    }
    
    public partial class TestRunner
    {
        private static TestLogger _testLogger;
        private static int _errorCount;

        public delegate int g__getAssertCallCount<TObject>(object obj, ref MyClass<TObject> P_1) where TObject : new();

        public static void Run()
        {
            _errorCount = 0;
            _testLogger = new TestLogger();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            LiveSharpRuntime.Logger = _testLogger;
            
            var ws = new LiveSharpWorkspace(_testLogger, ((b, c, a) => { })) {IsDryRunEnabled = false};
            var cd = Environment.CurrentDirectory;
            
            Console.WriteLine(cd);

            var projectInfo = new Shared.Api.ProjectInfo() {
                NuGetPackagePath = Path.GetFullPath(cd + @"/../../../../../build"),
                SolutionPath = Path.GetFullPath(cd + @"/../../../../../LiveSharp.sln"),
                ProjectDir = Path.GetFullPath(cd + @"/../../../../../test/LiveSharp.RuntimeTests"),
                ProjectName = "LiveSharp.RuntimeTests" 
            };
            
            ws.LoadSolution(projectInfo, false)
                .Wait();

            if (ws.Workspace == null)
                throw new Exception("Errors loading the solution. Please check the log.");

            var project = ws.Workspace.CurrentSolution.Projects.FirstOrDefault(p => p.Name == "LiveSharp.RuntimeTests");
            if (project == null) {
                _testLogger.LogError("Project LiveSharp.RuntimeTests not found");
                return;
            }

            InitXamarinForms();
            
            var compilation = project.GetCompilationAsync().Result;
            
            var assemblyForSending = LiveSharpHandler.CreateDynamicAssembly(compilation, project, _testLogger);
            
            var assembly = assemblyForSending.AssemblyDefinitionOriginal;
            var processor = new AssemblyUpdateProcessor(null, assembly, _testLogger);
            var diff = processor.CreateDiff();
            var documentSerializer = new DocumentSerializer(diff, new TypeRegistry(new RewriteLogger(_testLogger.LogMessage, _testLogger.LogWarning, _testLogger.LogError, _testLogger.LogError)), assembly.FullName);
            var serializedDocument = documentSerializer.Serialize();
            var serializedDocumentString = serializedDocument.ToString();
            
            LiveSharpRuntime.UpdateDocument(serializedDocument, debuggingEnabled: false);
            LiveSharpDebugger.StartSending();

            TestRuntimeUpdate<GenericsTests>(assembly, project, "Tests\\GenericsTests.cs", "Tests\\GenericsTests.cs");

            RunTestsOn<AsyncTests>();
            RunTestsOn<BasicTests>();
            RunTestsOn<ExceptionHandlingTests>();
            RunTestsOn<StructTests>();
            RunTestsOn<DynamicTests>();
            RunTestsOn<TypeResolutionTests>();
            RunTestsOn<ExpressionParameterTests>();
            
            RunTestsOn<TupleTests>();
            
            RunTestsOn<InheritanceTests>();
            RunTestsOn<OperatorTests>();
            RunTestsOn<ConditionalAccessTests>();
            RunTestsOn<PatternMatchingTests>();
            RunTestsOn<ForTests>();
            RunTestsOn<MethodTests>();
            RunTestsOn<ReturnTests>();
            RunTestsOn<SwitchTests>();
            RunTestsOn<ConstructorTests>();
            RunTestsOn<ArrayTests>();
            RunTestsOn<LambdaTests>();
            RunTestsOn<DelegateTests>();
            RunTestsOn<WhileTests>();
            RunTestsOn<DoTests>();
            RunTestsOn<EventsTests>();
            
            RunTestsOn<NullableTests>();
            RunTestsOn<StringTests>();
            
            RunTestsOn<UsingTests>();
            RunTestsOn<LockTests>();
            RunTestsOn<LockTests>();
            RunTestsOn<ForeachTests>();
            RunTestsOn<DebuggerTests>();
            RunTestsOn<MyControl>();
            
            TestRuntimeUpdate<CtorUpdateTests>(assembly, project, "Tests\\CtorUpdateTests.cs","Tests\\CtorUpdateTests.cs");
            TestRuntimeUpdate<NewMembers>(assembly, project, "Tests\\NewMembers.cs","Tests\\NewMembers.cs");
            
            RunSequenceTests<MainPage>(assembly, project, "Sequences\\BubblesGame\\MainPage.xaml.cs");
            RunSequenceTests<DynamicIndex>(assembly, project, "Sequences\\DynamicIndex\\DynamicIndex.cs");
            RunSequenceTests<ChangeReturnValue>(assembly, project, "Sequences\\ChangeReturnValue\\ChangeReturnValue.cs");
            
            Console.WriteLine("Error count: " + _errorCount);
            
            stopwatch.Stop();
            
            Console.WriteLine(stopwatch.Elapsed);
            
            assemblyForSending.Dispose();
        }

        private static void RunSequenceTests<TInstance>(AssemblyDefinition originalAssembly, Project project, string sequenceStart) where TInstance : new()
        {
            var existingDocument = project.Documents.FirstOrDefault(d =>
                string.Equals(d.Name, sequenceStart, StringComparison.InvariantCultureIgnoreCase));
            
            // Are we on Mac?
            if (existingDocument == null)
                existingDocument = project.Documents.FirstOrDefault(d => d.Name == sequenceStart.Replace("\\", "/"));
            
            if (existingDocument == null)
                throw new Exception($"Document {sequenceStart} doesn't exist");

            project = project.RemoveDocument(existingDocument.Id);

            var instances = new ConcurrentDictionary<Type, object>();
            var instance = new TInstance();
            var counter = 1;
            
            _testLogger.LogMessage("Testing sequence " + sequenceStart);            
            
            while (true) {
                var nextDocumentPath = existingDocument.FilePath + "." + counter++;
                
                try {
                    if (!File.Exists(nextDocumentPath))
                        break;
                    
                    _testLogger.LogMessage("Applying sequence item " + nextDocumentPath);
                
                    var updatedDocument = project.AddDocument(sequenceStart, SourceText.From(File.ReadAllText(nextDocumentPath), Encoding.Unicode));
                    var root = updatedDocument.GetSyntaxRootAsync().Result;
                    var semanticModel = updatedDocument.GetSemanticModelAsync().Result;
                    var errors = semanticModel.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

                    if (errors.Length > 0) {
                        foreach (var error in errors) {
                            _testLogger.LogError(error.ToString());
                        }
                        throw new Exception("Compilation errors");
                    }

                    using var assemblyForSending = LiveSharpHandler.CreateDynamicAssembly(semanticModel.Compilation, project, _testLogger);
                    
                    var processor = new AssemblyUpdateProcessor(originalAssembly, assemblyForSending.AssemblyDefinitionOriginal, _testLogger);
                    var documentSerializer = new DocumentSerializer(processor.CreateDiff(), new TypeRegistry(new RewriteLogger(_testLogger.LogMessage, _testLogger.LogWarning, _testLogger.LogError, _testLogger.LogError)), assemblyForSending.AssemblyDefinitionOriginal.FullName);
                    var documentElement = documentSerializer.Serialize();
                    var documentMetadata = LiveSharpRuntime.UpdateDocument(documentElement, typeName => !typeName.StartsWith("Test"));
                    
                    if (instance is IRunnableSequence runnable)
                        runnable.Run();
                }
                catch (Exception e) {
                    _testLogger.LogError($"{nextDocumentPath} failed", e);
                    throw;
                }
            }
            
            _testLogger.LogMessage("Sequence ended");      
        }

        private static void TestRuntimeUpdate<TInstance>(AssemblyDefinition originalAssembly, Project project, string documentName, string filename)
            where TInstance : new()
        {
            var existingDocument = project.Documents.FirstOrDefault(d =>
                string.Equals(d.Name, documentName, StringComparison.InvariantCultureIgnoreCase));
            
            // Are we on Mac?
            if (existingDocument == null)
                existingDocument = project.Documents.FirstOrDefault(d => d.Name == filename.Replace("\\", "/"));
            
            if (existingDocument == null)
                throw new Exception($"Document {filename} doesn't exist");

            project = project.RemoveDocument(existingDocument.Id);
            
            var updatedDocument = project.AddDocument(documentName, SourceText.From(File.ReadAllText(existingDocument.FilePath + ".update"), Encoding.Unicode));
            // var root = updatedDocument.GetSyntaxRootAsync().Result;
            var semanticModel = updatedDocument.GetSemanticModelAsync().Result;
            var errors = semanticModel.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            
            if (errors.Length > 0)
                throw new Exception("Compilation errors");

            using var assemblyForSending = LiveSharpHandler.CreateDynamicAssembly(semanticModel.Compilation, project, _testLogger);
                    
            var processor = new AssemblyUpdateProcessor(originalAssembly, assemblyForSending.AssemblyDefinitionOriginal, _testLogger);
            var assemblyDiff = processor.CreateDiff();
            var documentSerializer = new DocumentSerializer(assemblyDiff, new TypeRegistry(new RewriteLogger(_testLogger.LogMessage, _testLogger.LogWarning, _testLogger.LogError, _testLogger.LogError)), assemblyForSending.AssemblyDefinitionOriginal.FullName);
            var documentElement = documentSerializer.Serialize();
            var documentMetadata = LiveSharpRuntime.UpdateDocument(documentElement, typeName => !typeName.StartsWith("Test"));
            
            foreach (var method in documentMetadata.UpdatedMethods) {
                // Now compare all the Test methods to their statically compiled counter parts
                if (!method.Name.StartsWith("Test"))
                    continue;
                
                method.DelegateBuilder.Invoke(new TInstance());
            }
        }

        private static void RunTestsOn<TObject>(bool enableDebugger = false) where TObject : new()
        {
            var testMethodInfos = typeof(TObject).GetMethods()
                .Where(mi => mi.Name.StartsWith("Test"))
                .ToDictionary(m => m.Name);

            foreach (var kvp in testMethodInfos) {
                var name = kvp.Key;
                var methodInfo = kvp.Value;
                
                if (!name.StartsWith("Test"))
                    continue;

                try {
                    var methodIdentifier = methodInfo.GetMethodIdentifier();
                    if (!LiveSharpRuntime.GetMethodUpdate(typeof(TObject).Assembly, methodIdentifier, out var virtualMethodInfo)) {
                        _testLogger.LogError($"Couldn't find method {methodIdentifier}");
                        continue;
                    }
                    
                    TestMethod<TObject>(c => methodInfo.Invoke(c, null), virtualMethodInfo.DelegateBuilder);

                    Log.WriteLine("Test passed " + name);
                }
                catch (Exception e) {
                    Log.WriteLine("Test failed " + name);
                    Log.WriteLine(e.ToString());

                    _errorCount++;
                }
            }
        }

        private static void TestMethod<TObject>(Func<TObject, object> compiledMethodCall, DelegateBuilder methodMetadata) where TObject : new()
        {
            var controlForStatic = new TObject();

            Log.WriteLine("Compiled call " + methodMetadata.MethodIdentifier);

            var staticResult = compiledMethodCall(controlForStatic);
            if (staticResult is Task staticTask) staticTask.Wait();

            var staticResultString = controlForStatic.AsString();
            var controlForDynamic = new TObject();

            Log.WriteLine("Runtime call " + typeof(TObject).Name + "." + methodMetadata.MethodIdentifier);

            var result = methodMetadata.Invoke(controlForDynamic);
            if (result is Task dynamicTask) {
                dynamicTask.Wait();
            }

            var dynamicResultString = controlForDynamic.AsString();
            if (dynamicResultString != staticResultString)
                throw new Exception("Not equal");

            var assertCallCountProp =
                typeof(TObject).GetProperty("AssertCallCount", BindingFlags.Public | BindingFlags.Instance);

            if (assertCallCountProp == null)
                throw new InvalidOperationException($"{typeof(TObject).Name} is missing AssertCallCount property");

            var s = getAssertCallCount(controlForStatic);
            var d = getAssertCallCount(controlForDynamic);
            if (s != d)
                throw new Exception($"Assert call count differs with static {s} and dynamic {d}");

            int getAssertCallCount(object obj) => (int)assertCallCountProp.GetValue(obj);
        }

        public static void InitXamarinForms()
        {
            Device.Info = new MockDeviceInfo();
            Device.PlatformServices = new MockPlatformServices();
            DependencyService.Register<MockResourcesProvider>();
            DependencyService.Register<MockDeserializer>();
        }
    }
}