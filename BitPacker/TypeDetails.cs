using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class TypeDetails
    {
        private readonly int minSize;
        private readonly bool hasFixedSize;
        private readonly Expression operationExpression;

        public int MinSize
        {
            get { return this.minSize; }
        }

        public bool HasFixedSize
        {
            get { return this.hasFixedSize; }
        }

        public Expression OperationExpression
        {
            get { return this.operationExpression; }
        }

        public TypeDetails(bool hasFixedSize, int minSize, Expression operationExpression)
        {
            this.hasFixedSize = hasFixedSize;
            this.minSize = minSize;
            this.operationExpression = operationExpression;
        }
    }
}
