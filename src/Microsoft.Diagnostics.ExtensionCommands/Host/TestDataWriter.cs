using Microsoft.Diagnostics.DebugServices;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    public class TestDataWriter
    {
        public readonly XElement Root;
        public readonly XElement Target;

        /// <summary>
        /// Write a test data file from the target
        /// </summary>
        public TestDataWriter()
        {
            Root = new XElement("TestData");
            Target = new XElement("Target");
            Root.Add(Target);
        }

        public void Build(ITarget target)
        {
            AddMembers(Target, typeof(ITarget), target, nameof(ITarget.Id), nameof(ITarget.GetTempDirectory));

            var modulesElement = new XElement("Modules");
            Target.Add(modulesElement);

            var moduleService = target.Services.GetService<IModuleService>();
            foreach (IModule module in moduleService.EnumerateModules())
            {
                var moduleElement = new XElement("Module");
                modulesElement.Add(moduleElement);
                AddModuleMembers(moduleElement, module);
            }

            var threadsElement = new XElement("Threads");
            Target.Add(threadsElement);

            var threadService = target.Services.GetService<IThreadService>();
            var registerIndexes = new int[] { threadService.InstructionPointerIndex, threadService.StackPointerIndex, threadService.FramePointerIndex };
            foreach (IThread thread in threadService.EnumerateThreads())
            {
                var threadElement = new XElement("Thread");
                threadsElement.Add(threadElement);
                AddMembers(threadElement, typeof(IThread), thread, nameof(IThread.ThreadIndex), nameof(IThread.GetThreadContext));

                var registersElement = new XElement("Registers");
                threadElement.Add(registersElement);
                foreach (int registerIndex in registerIndexes)
                {
                    var registerElement = new XElement("Register");
                    registersElement.Add(registerElement);

                    if (threadService.TryGetRegisterInfo(registerIndex, out RegisterInfo info))
                    {
                        AddMembers(registerElement, typeof(RegisterInfo), info, nameof(Object.ToString), nameof(Object.GetHashCode));
                    }
                    if (thread.TryGetRegisterValue(registerIndex, out ulong value))
                    {
                        registerElement.Add(new XElement("Value", $"0x{value:X16}"));
                    }
                }
            }

            var runtimesElement = new XElement("Runtimes");
            Target.Add(runtimesElement);

            var runtimeService = target.Services.GetService<IRuntimeService>();
            foreach (IRuntime runtime in runtimeService.EnumerateRuntimes())
            {
                var runtimeElement = new XElement("Runtime");
                runtimesElement.Add(runtimeElement);
                AddMembers(runtimeElement, typeof(IRuntime), runtime, nameof(IRuntime.GetDacFilePath), nameof(IRuntime.GetDbiFilePath));

                var runtimeModuleElement = new XElement("RuntimeModule");
                runtimeElement.Add(runtimeModuleElement);
                AddModuleMembers(runtimeModuleElement, runtime.RuntimeModule);
            }
        }

        public void Write(string testDataFile)
        {
            File.WriteAllText(testDataFile, Root.ToString());
        }

        private void AddModuleMembers(XElement element, IModule module)
        {
            AddMembers(element, typeof(IModule), module, nameof(IModule.ModuleIndex), nameof(IModule.VersionString));
        }

        private void AddMembers(XElement element, Type type, object instance, params string[] membersToSkip)
        {
            MemberInfo[] members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance);
            foreach (MemberInfo member in members)
            {
                if (membersToSkip.Any((skip) => member.Name == skip)) {
                    continue;
                }
                string result = null;
                object memberValue = null;
                Type memberType = null;

                switch (member.MemberType)
                {
                    case MemberTypes.Property:
                        memberValue = ((PropertyInfo)member).GetValue(instance);
                        memberType = ((PropertyInfo)member).PropertyType;
                        break;
                    case MemberTypes.Field:
                        memberValue = ((FieldInfo)member).GetValue(instance);
                        memberType = ((FieldInfo)member).FieldType;
                        break;
                    case MemberTypes.Method:
                        MethodInfo methodInfo = (MethodInfo)member;
                        if (!methodInfo.IsSpecialName && methodInfo.GetParameters().Length == 0 && methodInfo.ReturnType != typeof(void))
                        {
                            memberValue = ((MethodInfo)member).Invoke(instance, null);
                            memberType = ((MethodInfo)member).ReturnType;
                        }
                        break;
                }
                if (memberType != null)
                {
                    Type nullableType = Nullable.GetUnderlyingType(memberType);
                    memberType = nullableType ?? memberType;

                    if (nullableType != null && memberValue == null)
                    {
                        result = "";
                    }
                    else if (memberType == typeof(string))
                    {
                        result = (string)memberValue ?? "";
                    }
                    else if (memberType == typeof(bool))
                    {
                        result = (bool)memberValue ? "true" : "false";
                    }
                    else if (memberValue is ImmutableArray<byte> buildId)
                    {
                        if (!buildId.IsDefaultOrEmpty)
                        {
                            result = string.Concat(buildId.Select((b) => b.ToString("x2")));
                        }
                    }
                    else if (memberType.IsEnum)
                    {
                        result = memberValue.ToString();
                    }
                    else if (memberType.IsPrimitive)
                    {
                        if (memberType == typeof(short) || memberType == typeof(int) || memberType == typeof(long))
                        {
                            result = memberValue.ToString();
                        }
                        else
                        {
                            int digits = Marshal.SizeOf(memberType) * 2;
                            result = string.Format($"0x{{0:X{digits}}}", memberValue);
                        }
                    }
                    else if (memberType.IsValueType)
                    {
                        result = memberValue.ToString();
                    }
                }
                if (result != null)
                {
                    element.Add(new XElement(member.Name, result));
                }
            }
        }
    }
}
