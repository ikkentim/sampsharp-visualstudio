﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SampSharp.VisualStudio.DebugEngine.Enumerators;
using SampSharp.VisualStudio.DebugEngine.Events;
using SampSharp.VisualStudio.Debugger;
using SampSharp.VisualStudio.Utils;
using static Microsoft.VisualStudio.VSConstants;

namespace SampSharp.VisualStudio.DebugEngine
{
    [Guid("D78CF801-CE2A-499B-BF1F-C81742877A34")]
    public class MonoEngine : IDebugEngine2, IDebugProgram3, IDebugEngineLaunch2, IDebugSymbolSettings100
    {
        private readonly MonoThreadManager _threadManager;
        private IVsOutputWindowPane _outputWindow;

        public MonoEngine()
        {
            var breakpointManager = new MonoBreakpointManager(this);
            _threadManager = new MonoThreadManager(this);

            Program = new DebuggedProgram(this, breakpointManager, _threadManager);
        }
        
        public MonoCallback Callback { get; private set; }
        public DebuggedProgram Program { get; }

        public IVsOutputWindowPane OutputWindow => _outputWindow;

        #region Implementation of IDebugSymbolSettings100

        /// <summary>
        ///     The SDM will call this method on the debug engine when it is created, to notify it of the user's
        ///     symbol settings in Tools->Options->Debugging->Symbols.
        /// </summary>
        /// <param name="isManual">true if 'Automatically load symbols: Only for specified modules' is checked.</param>
        /// <param name="loadAdjacentSymbols">true if 'Specify modules'->'Always load symbols next to the modules' is checked.</param>
        /// <param name="includeList">semicolon-delimited list of modules when automatically loading 'Only specified modules'</param>
        /// <param name="excludeList">
        ///     semicolon-delimited list of modules when automatically loading 'All modules, unless
        ///     excluded'.
        /// </param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int SetSymbolLoadState(int isManual, int loadAdjacentSymbols, string includeList, string excludeList)
        {
            return S_OK;
        }

        #endregion

        #region Methods of MonoEngine

        /// <summary>
        ///     Gets the document name for the document at the pointer.
        /// </summary>
        /// <param name="locationPtr"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public string GetDocumentName(IntPtr locationPtr)
        {
            TEXT_POSITION[] startPosition;
            TEXT_POSITION[] endPosition;
            return GetLocationInfo(locationPtr, out startPosition, out endPosition);
        }

        /// <summary>
        ///     Gets the location information for the specified document position.
        /// </summary>
        /// <param name="locationPtr"></param>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public string GetLocationInfo(IntPtr locationPtr, out TEXT_POSITION[] startPosition,
            out TEXT_POSITION[] endPosition)
        {
            var docPosition = (IDebugDocumentPosition2) Marshal.GetObjectForIUnknown(locationPtr);
            var result = GetLocationInfo(docPosition, out startPosition, out endPosition);
            Marshal.ReleaseComObject(docPosition);
            return result;
        }

        /// <summary>
        ///     Gets the location information for the specifed document position.
        /// </summary>
        /// <param name="docPosition"></param>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public string GetLocationInfo(IDebugDocumentPosition2 docPosition, out TEXT_POSITION[] startPosition,
            out TEXT_POSITION[] endPosition)
        {
            string documentName;
            EngineUtils.CheckOk(docPosition.GetFileName(out documentName));

            startPosition = new TEXT_POSITION[1];
            endPosition = new TEXT_POSITION[1];
            EngineUtils.CheckOk(docPosition.GetRange(startPosition, endPosition));

            return documentName;
        }
        
        private void OnStartDebuggingFailed(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return; // don't show a message in this case
            }

            var message = exception.Message;

            var initializationException = exception as DebuggerInitializeException;
            if (initializationException != null)
                HostOutputWindow.WriteLaunchError(message + Environment.NewLine);

            Callback.OnErrorImmediate(message);
        }
        #endregion

        #region Implementation of IDebugEngine2

        /// <summary>
        ///     Creates a pending breakpoint in the engine. A pending breakpoint is contains all the information needed to bind a
        ///     breakpoint to
        ///     a location in the debuggee.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="pendingBreakpoint"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int CreatePendingBreakpoint(IDebugBreakpointRequest2 request,
            out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            try
            {
                pendingBreakpoint = Program.CreatePendingBreakpoint(request);

                return S_OK;
            }
            catch
            {
                pendingBreakpoint = null;
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Specifies how the DE should handle a given exception.
        ///     The sample engine does not support exceptions in the debuggee so this method is not actually implemented.
        /// </summary>
        /// <param name="pException"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int SetException(EXCEPTION_INFO[] pException)
        {
            try
            {
                Program.SetException(pException[0]);
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="pException"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int RemoveSetException(EXCEPTION_INFO[] pException)
        {
            try
            {
                Program.RemoveSetException(pException[0]);
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Removes the list of exceptions the IDE has set for a particular run-time architecture or language.
        /// </summary>
        /// <param name="guidType"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int RemoveAllSetExceptions(ref Guid guidType)
        {
            try
            {
                Program.RemoveAllSetExceptions();
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Gets the GUID of the DE.
        /// </summary>
        /// <param name="engineGuid">The unique identifier of the DE.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetEngineId(out Guid engineGuid)
        {
            engineGuid = Guids.EngineIdGuid;
            return S_OK;
        }

        /// <summary>
        ///     Informs a DE that the program specified has been atypically terminated and that the DE should
        ///     clean up all references to the program and send a program destroy event.
        /// </summary>
        /// <param name="pProgram">The p program.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int DestroyProgram(IDebugProgram2 pProgram)
        {
            return E_NOTIMPL;
        }

        /// <summary>
        ///     Called by the SDM to indicate that a synchronous debug event, previously sent by the DE to the SDM,
        ///     was received and processed. The only event the sample engine sends in this fashion is Program Destroy.
        ///     It responds to that event by shutting down the engine.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int ContinueFromSynchronousEvent(IDebugEvent2 @event)
        {
            if (@event is SampSharpDestroyEvent)
                Program.Dispose();

            return S_OK;
        }

        /// <summary>
        ///     Sets the locale of the DE.
        ///     This method is called by the session debug manager (SDM) to propagate the locale settings of the IDE so that
        ///     strings returned by the DE are properly localized. The sample engine is not localized so this is not implemented.
        /// </summary>
        /// <param name="languageId">The language identifier.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int SetLocale(ushort languageId)
        {
            return S_OK;
        }

        /// <summary>
        ///     Sets the registry root currently in use by the DE. Different installations of Visual Studio can change where their
        ///     registry information is stored
        ///     This allows the debugger to tell the engine where that location is.
        /// </summary>
        /// <param name="registryRoot">The registry root.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int SetRegistryRoot(string registryRoot)
        {
            return S_OK;
        }

        /// <summary>
        ///     A metric is a registry value used to change a debug engine's behavior or to advertise supported functionality.
        ///     This method can forward the call to the appropriate form of the Debugging SDK Helpers function, SetMetric.
        /// </summary>
        /// <param name="metric">The metric.</param>
        /// <param name="value">The value.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int SetMetric(string metric, object value)
        {
            return S_OK;
        }

        /// <summary>
        ///     The debugger calls CauseBreak when the user clicks on the pause button in VS. The debugger should respond by
        ///     entering
        ///     breakmode.
        /// </summary>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int CauseBreak()
        {
            try
            {
                Program.Break();
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Attach the debug engine to a program.
        /// </summary>
        /// <param name="programs"></param>
        /// <param name="rgpProgramNodes">.</param>
        /// <param name="celtPrograms"></param>
        /// <param name="pCallback"></param>
        /// <param name="dwReason"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int Attach(IDebugProgram2[] programs, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms,
            IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
        {
            try
            {
                Program.Attach(programs[0]);
                return S_OK;
            }
            catch (Exception e)
            {
                OnStartDebuggingFailed(e);
                return E_FAIL;
            }
        }

        #endregion

        #region Implementation of IDebugProgram2

        /// <summary>
        ///     EnumThreads is called by the debugger when it needs to enumerate the threads in the program.
        /// </summary>
        /// <param name="ppEnum"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            ppEnum = new MonoThreadEnumerator(_threadManager.All
                .ToArray()
                .OfType<IDebugThread2>()
                .ToArray());

            return S_OK;
        }

        /// <summary>
        ///     Gets the name of the program.
        ///     The name returned by this method is always a friendly, user-displayable name that describes the program.
        /// </summary>
        /// <param name="programName">Name of the program.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetName(out string programName)
        {
            programName = null;
            return S_OK;
        }

        /// <summary>
        ///     Terminates the program.
        /// </summary>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int Terminate()
        {
            return S_OK;
        }

        /// <summary>
        ///     Determines if a debug engine (DE) can detach from the program.
        /// </summary>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int CanDetach()
        {
            return S_OK;
        }

        /// <summary>
        ///     Detach is called when debugging is stopped and the process was attached to (as opposed to launched)
        ///     or when one of the Detach commands are executed in the UI.
        /// </summary>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int Detach()
        {
            try
            {
                Program.Detach();
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Gets a GUID for this program. A debug engine (DE) must return the program identifier originally passed to the
        ///     IDebugProgramNodeAttach2::OnAttach
        ///     or IDebugEngine2::Attach methods. This allows identification of the program across debugger components.
        /// </summary>
        /// <param name="programId">The program identifier.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetProgramId(out Guid programId)
        {
            programId = Program.Id;
            return S_OK;
        }

        /// <summary>
        ///     The properties returned by this method are specific to the program. If the program needs to return more than one
        ///     property,
        ///     then the IDebugProperty2 object returned by this method is a container of additional properties and calling the
        ///     IDebugProperty2::EnumChildren method returns a list of all properties.
        ///     A program may expose any number and type of additional properties that can be described through the IDebugProperty2
        ///     interface.
        ///     An IDE might display the additional program properties through a generic property browser user interface.
        ///     The sample engine does not support this
        /// </summary>
        /// <param name="ppProperty"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            ppProperty = null;

            return E_NOTIMPL;
        }

        /// <summary>
        ///     Gets the name and identifier of the debug engine (DE) running this program.
        /// </summary>
        /// <param name="pbstrEngine"></param>
        /// <param name="pguidEngine"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        {
            // TODO: Implement
            pbstrEngine = null;
            pguidEngine = Guid.Empty;

            return E_NOTIMPL;
        }

        /// <summary>
        ///     The memory bytes as represented by the IDebugMemoryBytes2 object is for the program's image in memory and not any
        ///     memory
        ///     that was allocated when the program was executed.
        /// </summary>
        /// <param name="ppMemoryBytes"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            ppMemoryBytes = null;

            return E_NOTIMPL;
        }

        /// <summary>
        ///     The debugger calls this when it needs to obtain the IDebugDisassemblyStream2 for a particular code-context.
        ///     The sample engine does not support dissassembly so it returns E_NOTIMPL
        ///     In order for this to be called, the Disassembly capability must be set in the registry for this Engine
        /// </summary>
        /// <param name="dwScope"></param>
        /// <param name="pCodeContext"></param>
        /// <param name="ppDisassemblyStream"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext,
            out IDebugDisassemblyStream2 ppDisassemblyStream)
        {
            ppDisassemblyStream = null;
            return E_NOTIMPL;
        }

        /// <summary>
        ///     EnumModules is called by the debugger when it needs to enumerate the modules in the program.
        /// </summary>
        /// <param name="ppEnum"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int EnumModules(out IEnumDebugModules2 ppEnum)
        {
            ppEnum = new MonoModuleEnumerator(new[] { Program.Module });
            return S_OK;
        }

        /// <summary>
        ///     This method gets the Edit and Continue (ENC) update for this program. A custom debug engine always returns
        ///     E_NOTIMPL
        /// </summary>
        /// <param name="ppUpdate"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetENCUpdate(out object ppUpdate)
        {
            ppUpdate = null;
            return E_NOTIMPL;
        }

        /// <summary>
        ///     EnumCodePaths is used for the step-into specific feature -- right click on the current statment and decide which
        ///     function to step into. This is not something that the SampleEngine supports.
        /// </summary>
        /// <param name="hint">The hint.</param>
        /// <param name="start">The start.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="fSource">The f source.</param>
        /// <param name="pathEnum">The path enum.</param>
        /// <param name="safetyContext">The safety context.</param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int EnumCodePaths(string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int fSource,
            out IEnumCodePaths2 pathEnum, out IDebugCodeContext2 safetyContext)
        {
            pathEnum = null;
            safetyContext = null;
            return E_NOTIMPL;
        }

        /// <summary>
        ///     Writes a dump to a file.
        /// </summary>
        /// <param name="dumptype"></param>
        /// <param name="pszDumpUrl"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int WriteDump(enum_DUMPTYPE dumptype, string pszDumpUrl)
        {
            return E_NOTIMPL;
        }

        #endregion

        #region Implementation of IDebugProgram3

        /// <summary>
        ///     Steps to the next statement.
        /// </summary>
        /// <param name="thread"></param>
        /// <param name="kind"></param>
        /// <param name="unit"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int Step(IDebugThread2 thread, enum_STEPKIND kind, enum_STEPUNIT unit)
        {
            try
            {
                Program.Step(kind, unit);
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     Enumerates the code contexts for a given position in a source file.
        /// </summary>
        /// <param name="pDocPos"></param>
        /// <param name="ppEnum"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
        {
            TEXT_POSITION[] startPosition;
            TEXT_POSITION[] endPosition;
            var documentName = GetLocationInfo(pDocPos, out startPosition, out endPosition);

            var textPosition = new TEXT_POSITION { dwLine = startPosition[0].dwLine + 1 };
            var documentContext = new MonoDocumentContext(documentName, textPosition, textPosition, null);
            ppEnum = new MonoCodeContextEnumerator(new IDebugCodeContext2[]
                { new MonoMemoryAddress(this, 0, documentContext) });
            return S_OK;
        }

        /// <summary>
        ///     Continue is called from the SDM when it wants execution to continue in the debugee
        ///     but have stepping state remain. An example is when a tracepoint is executed,
        ///     and the debugger does not want to actually enter break mode.
        /// </summary>
        /// <param name="pThread"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int Continue(IDebugThread2 pThread)
        {
            try
            {
                Program.Continue();

                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        /// <summary>
        ///     ExecuteOnThread is called when the SDM wants execution to continue and have
        ///     stepping state cleared.
        /// </summary>
        /// <param name="pThread"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int ExecuteOnThread(IDebugThread2 pThread)
        {
            try
            {
                Program.ExecuteOnThread(pThread);
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        #endregion

        #region Implementation of IDebugEngineLaunch2

        /// <summary>
        ///     Launches a process by means of the debug engine.
        ///     Normally, Visual Studio launches a program using the IDebugPortEx2::LaunchSuspended method and then attaches the
        ///     debugger
        ///     to the suspended program. However, there are circumstances in which the debug engine may need to launch a program
        ///     (for example, if the debug engine is part of an interpreter and the program being debugged is an interpreted
        ///     language),
        ///     in which case Visual Studio uses the IDebugEngineLaunch2::LaunchSuspended method
        ///     The IDebugEngineLaunch2::ResumeProcess method is called to start the process after the process has been
        ///     successfully launched in a suspended state.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <param name="directory"></param>
        /// <param name="environment"></param>
        /// <param name="options"></param>
        /// <param name="launchFlags"></param>
        /// <param name="standardInput"></param>
        /// <param name="standardOutput"></param>
        /// <param name="standardError"></param>
        /// <param name="callback"></param>
        /// <param name="process"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int LaunchSuspended(string server, IDebugPort2 port, string exe, string args, string directory,
            string environment, string options, enum_LAUNCH_FLAGS launchFlags, uint standardInput, uint standardOutput,
            uint standardError, IDebugEventCallback2 callback, out IDebugProcess2 process)
        {
            var outputWindow = (IVsOutputWindow) Package.GetGlobalService(typeof(SVsOutputWindow));
            var generalPaneGuid = GUID_OutWindowDebugPane;
            outputWindow.GetPane(ref generalPaneGuid, out _outputWindow);
            
            Callback = new MonoCallback(callback, this);
            try
            {
                var opt = options.Split(new[] { "|split|" }, StringSplitOptions.None);

                if (opt.Length != 2)
                    throw new Exception("Invalid launch options");

                bool noWindow;
                bool.TryParse(opt[1], out noWindow);

                Program.LaunchSuspended(port, opt[0], noWindow, exe, directory, out process);

                return S_OK;
            }
            catch (Exception e)
            {
                OnStartDebuggingFailed(e);
                process = null;
                return E_ABORT;
            }
        }


        /// <summary>
        ///     Resume a process launched by IDebugEngineLaunch2.LaunchSuspended
        /// </summary>
        /// <param name="process"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int ResumeProcess(IDebugProcess2 process)
        {
            IDebugPort2 port;
            EngineUtils.RequireOk(process.GetPort(out port));

            var defaultPort = (IDebugDefaultPort2) port;
            IDebugPortNotify2 portNotify;
            EngineUtils.RequireOk(defaultPort.GetPortNotify(out portNotify));

            EngineUtils.RequireOk(portNotify.AddProgramNode(Program.Node));

            return S_OK;
        }

        /// <summary>
        ///     Determines if a process can be terminated.
        /// </summary>
        /// <param name="pProcess"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int CanTerminateProcess(IDebugProcess2 pProcess)
        {
            return S_OK;
        }

        /// <summary>
        ///     This function is used to terminate a process that the SampleEngine launched
        ///     The debugger will call IDebugEngineLaunch2::CanTerminateProcess before calling this method
        /// </summary>
        /// <param name="pProcess"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int TerminateProcess(IDebugProcess2 pProcess)
        {
            pProcess.Terminate();
            Callback.Send(new SampSharpDestroyEvent(0), SampSharpDestroyEvent.Iid, null);
            return S_OK;
        }

        #endregion

        #region Deprecated interface methods

        public int EnumPrograms(out IEnumDebugPrograms2 programs)
        {
            programs = null;

            Debug.Fail("This function is not called by the debugger");

            return E_NOTIMPL;
        }

        public int Attach(IDebugEventCallback2 pCallback)
        {
            Debug.Fail("This function is not called by the debugger");

            return E_NOTIMPL;
        }

        public int GetProcess(out IDebugProcess2 process)
        {
            process = null;

            Debug.Fail("This function is not called by the debugger");

            return E_NOTIMPL;
        }

        public int Execute()
        {
            Debug.Fail("This function is not called by the debugger");

            return E_NOTIMPL;
        }

        #endregion
    }
}