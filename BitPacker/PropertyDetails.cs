using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class PropertyDetails
    {
        public readonly BitPackerMemberAttribute Attribute;
        public readonly PropertyInfo PropertyInfo;

        public Type PropertyType
        {
            get { return this.PropertyInfo.PropertyType; }
        }

        public PropertyDetails(PropertyInfo propertyInfo, BitPackerMemberAttribute attribute)
        {
            this.PropertyInfo = propertyInfo;
            this.Attribute = attribute;
        }
    }
}
