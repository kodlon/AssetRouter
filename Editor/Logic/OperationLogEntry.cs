using System;
using System.Collections.Generic;

namespace Kodlon.AssetRouter.Logic
{
    [Serializable]
    internal sealed class OperationLogEntry
    {
        public string from;
        public string to;
        public string ruleName;

        public OperationLogEntry() { }

        public OperationLogEntry(string from, string to, string ruleName)
        {
            this.from     = from;
            this.to       = to;
            this.ruleName = ruleName;
        }
    }

    [Serializable]
    internal sealed class OperationSession
    {
        public string timestamp;
        public string source;
        public List<OperationLogEntry> entries;
    }

    [Serializable]
    internal sealed class OperationLogFile
    {
        public int v = 1;
        public List<OperationSession> sessions = new List<OperationSession>();
    }
}
