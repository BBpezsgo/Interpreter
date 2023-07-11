using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProgrammingLanguage.Tester.Parser.Statements
{
    using Core;

    public abstract class Statement
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Position position;

        public override string ToString()
        { return this.GetType().Name; }

        public abstract string PrettyPrint(int ident = 0);

        public virtual object TryGetValue()
        { return null; }

        public abstract bool TryGetTotalPosition(out Position result);

        internal abstract void SetPosition();
    }
}
