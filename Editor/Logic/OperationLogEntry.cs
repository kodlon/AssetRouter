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

        // Undo cleanup targets. Null on v=1 sessions; OperationLog.ReadAll normalises to empty lists.
        public List<string> createdAssets;
        public List<string> createdFolders;
    }

    [Serializable]
    internal sealed class OperationLogFile
    {
        // v2 added createdAssets / createdFolders. Older files parse fine — missing fields land as null.
        public int v = 2;
        public List<OperationSession> sessions = new List<OperationSession>();
    }
}
