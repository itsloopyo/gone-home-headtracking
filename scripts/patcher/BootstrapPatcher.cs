using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class BootstrapPatcher
{
    private const string PatchMarker = "HeadTracking_Patched_GoneHome_v4";
    private const string BootstrapTypeName = "HeadTrackingBootstrap";

    public static bool PatchAssembly(string assemblyPath)
    {
        string managedDir = Path.GetDirectoryName(assemblyPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        using (var memStream = new MemoryStream(assemblyBytes))
        using (var assembly = AssemblyDefinition.ReadAssembly(memStream, readerParams))
        {
            if (assembly.MainModule.Types.Any(t => t.Name == PatchMarker))
            {
                Console.WriteLine("  Assembly already patched - skipping");
                return true;
            }

            // Create bootstrap class with static Initialize method that uses reflection
            var bootstrapType = new TypeDefinition(
                "HeadTracking",
                BootstrapTypeName,
                TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract,
                assembly.MainModule.TypeSystem.Object);

            var initializedField = new FieldDefinition(
                "_initialized",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Boolean);
            bootstrapType.Fields.Add(initializedField);

            var initMethod = new MethodDefinition(
                "Initialize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void);

            var il = initMethod.Body.GetILProcessor();
            initMethod.Body.InitLocals = true;

            // Local variables
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String)); // 0: managedDir
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String)); // 1: dllPath
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 2: assembly
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 3: type
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 4: method
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 5: exception

            // Import required methods from mscorlib
            var mscorlibRef = assembly.MainModule.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib");
            var mscorlib = resolver.Resolve(mscorlibRef);

            var assemblyType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Reflection.Assembly");
            var loadFromRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "LoadFrom" && m.Parameters.Count == 1));
            var getTypeRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "GetType" && m.Parameters.Count == 1));
            var getLocationRef = assembly.MainModule.ImportReference(
                assemblyType.Properties.First(p => p.Name == "Location").GetMethod);
            var getExecutingAssemblyRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "GetExecutingAssembly"));

            var typeType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Type");
            var getMethodRef = assembly.MainModule.ImportReference(
                typeType.Methods.First(m => m.Name == "GetMethod" && m.Parameters.Count == 1));

            var methodBaseType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Reflection.MethodBase");
            var invokeRef = assembly.MainModule.ImportReference(
                methodBaseType.Methods.First(m => m.Name == "Invoke" && m.Parameters.Count == 2));

            var pathType = mscorlib.MainModule.Types.First(t => t.FullName == "System.IO.Path");
            var getDirectoryNameRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "GetDirectoryName"));
            var combineRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "Combine" && m.Parameters.Count == 2));
            var getTempPathRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "GetTempPath"));

            var exceptionType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Exception");
            var toStringRef = assembly.MainModule.ImportReference(
                exceptionType.Methods.First(m => m.Name == "ToString" && m.Parameters.Count == 0));

            var fileType = mscorlib.MainModule.Types.First(t => t.FullName == "System.IO.File");
            var appendAllTextRef = assembly.MainModule.ImportReference(
                fileType.Methods.First(m => m.Name == "AppendAllText" && m.Parameters.Count == 2));

            var stringType = mscorlib.MainModule.Types.First(t => t.FullName == "System.String");
            var concatRef = assembly.MainModule.ImportReference(
                stringType.Methods.First(m => m.Name == "Concat" && m.Parameters.Count == 2
                    && m.Parameters[0].ParameterType.FullName == "System.String"));

            // Build the method body
            var retInstruction = il.Create(OpCodes.Ret);
            var tryStart = il.Create(OpCodes.Nop);
            var catchStart = il.Create(OpCodes.Nop);

            // Check if already initialized — fast path
            il.Append(il.Create(OpCodes.Ldsfld, initializedField));
            il.Append(il.Create(OpCodes.Brtrue, retInstruction));

            // Set initialized = true
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Stsfld, initializedField));

            // try {
            il.Append(tryStart);

            // managedDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            il.Append(il.Create(OpCodes.Call, getExecutingAssemblyRef));
            il.Append(il.Create(OpCodes.Callvirt, getLocationRef));
            il.Append(il.Create(OpCodes.Call, getDirectoryNameRef));
            il.Append(il.Create(OpCodes.Stloc_0));

            // dllPath = Path.Combine(managedDir, "HeadTracking.dll")
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "HeadTracking.dll"));
            il.Append(il.Create(OpCodes.Call, combineRef));
            il.Append(il.Create(OpCodes.Stloc_1));

            // File.AppendAllText(managedDir + "\HeadTracking_BOOT.log", "Loading HeadTracking.dll...\n")
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "Loading HeadTracking.dll...\n"));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            // assembly = Assembly.LoadFrom(dllPath)
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Call, loadFromRef));
            il.Append(il.Create(OpCodes.Stloc_2));

            // type = assembly.GetType("HeadTracking.ModLoader")
            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldstr, "HeadTracking.ModLoader"));
            il.Append(il.Create(OpCodes.Callvirt, getTypeRef));
            il.Append(il.Create(OpCodes.Stloc_3));

            // method = type.GetMethod("Initialize")
            il.Append(il.Create(OpCodes.Ldloc_3));
            il.Append(il.Create(OpCodes.Ldstr, "Initialize"));
            il.Append(il.Create(OpCodes.Callvirt, getMethodRef));
            il.Append(il.Create(OpCodes.Stloc, 4));

            // method.Invoke(null, null)
            il.Append(il.Create(OpCodes.Ldloc, 4));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, invokeRef));
            il.Append(il.Create(OpCodes.Pop));

            // Log success
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "SUCCESS: ModLoader.Initialize() called\n"));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            var leaveTarget = il.Create(OpCodes.Ret);
            il.Append(il.Create(OpCodes.Leave, leaveTarget));

            // } catch (Exception ex) {
            il.Append(catchStart);
            il.Append(il.Create(OpCodes.Stloc, 5));

            // Log error to temp path (managedDir may not be set)
            il.Append(il.Create(OpCodes.Call, getTempPathRef));
            il.Append(il.Create(OpCodes.Ldstr, "HeadTracking_BOOT_ERROR.log"));
            il.Append(il.Create(OpCodes.Call, combineRef));

            il.Append(il.Create(OpCodes.Ldstr, "ERROR: "));
            il.Append(il.Create(OpCodes.Ldloc, 5));
            il.Append(il.Create(OpCodes.Callvirt, toStringRef));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "\n"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            il.Append(il.Create(OpCodes.Leave, leaveTarget));
            // }

            il.Append(leaveTarget);

            // Add exception handler
            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = tryStart,
                TryEnd = catchStart,
                HandlerStart = catchStart,
                HandlerEnd = leaveTarget,
                CatchType = assembly.MainModule.ImportReference(exceptionType)
            };
            initMethod.Body.ExceptionHandlers.Add(handler);

            bootstrapType.Methods.Add(initMethod);
            assembly.MainModule.Types.Add(bootstrapType);

            // Find target type to inject into
            string[] targetTypes = { "PlatformManager", "vp_FPSPlayer", "vp_FPSCamera", "vp_FPSController" };
            TypeDefinition targetType = null;
            string targetTypeName = null;

            foreach (var typeName in targetTypes)
            {
                targetType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == typeName);
                if (targetType != null)
                {
                    targetTypeName = typeName;
                    break;
                }
            }

            if (targetType == null)
            {
                Console.WriteLine("  ERROR: Could not find any target type to patch");
                return false;
            }

            Console.WriteLine("  Found target type: " + targetTypeName);

            // Inject into Start() or Awake()
            var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Start" && !m.IsStatic && m.HasBody);
            if (targetMethod == null)
                targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Awake" && !m.IsStatic && m.HasBody);

            if (targetMethod == null)
            {
                Console.WriteLine("  ERROR: Could not find Start or Awake method in " + targetTypeName);
                return false;
            }

            // Inject call to bootstrap at start of method
            var targetIL = targetMethod.Body.GetILProcessor();
            var firstInstruction = targetMethod.Body.Instructions.First();
            targetIL.InsertBefore(firstInstruction, targetIL.Create(OpCodes.Call, initMethod));
            Console.WriteLine("  Injected HeadTrackingBootstrap.Initialize() into " + targetTypeName + "." + targetMethod.Name);

            // Add marker type to prevent double-patching
            var markerType = new TypeDefinition(
                "HeadTracking",
                PatchMarker,
                TypeAttributes.NotPublic | TypeAttributes.Class,
                assembly.MainModule.TypeSystem.Object);
            assembly.MainModule.Types.Add(markerType);

            assembly.Write(assemblyPath);
            Console.WriteLine("  Successfully patched " + Path.GetFileName(assemblyPath));
            return true;
        }
    }
}
