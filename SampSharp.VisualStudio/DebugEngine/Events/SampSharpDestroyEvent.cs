﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace SampSharp.VisualStudio.DebugEngine.Events
{
    // This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a program has run to completion
    // or is otherwise destroyed.
    public class SampSharpDestroyEvent : SynchronousEvent, IDebugProgramDestroyEvent2
    {
        public const string Iid = "E147E9E3-6440-4073-A7B7-A65592C714B5";

        private readonly uint _exitCode;

        public SampSharpDestroyEvent(uint exitCode)
        {
            _exitCode = exitCode;
        }

        #region Implementation of IDebugProgramDestroyEvent2

        /// <summary>
        ///     Gets the program's exit code.
        /// </summary>
        /// <param name="exitCode"></param>
        /// <returns>If successful, returns S_OK; otherwise, returns an error code.</returns>
        public int GetExitCode(out uint exitCode)
        {
            exitCode = _exitCode;

            return VSConstants.S_OK;
        }

        #endregion
    }
}