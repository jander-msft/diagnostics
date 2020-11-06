// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal abstract class ExpressionNode
    {
        public abstract T Accept<T>(ExpressionNodeVisitor<T> visitor);

        private string DebuggerDisplay
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                Accept(new DebugDisplayVisitor(builder));
                return builder.ToString();
            }
        }
    }
}
