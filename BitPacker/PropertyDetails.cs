using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class PropertyAttributes
    {
        protected readonly BitPackerMemberAttribute attribute;
        protected readonly Endianness defaultEndianness;

        public Endianness Endianness
        {
            get { return this.attribute.NullableEndianness ?? this.defaultEndianness; }
        }

        public Type EnumType
        {
            get { return this.attribute.EnumType; }
        }

        public PropertyAttributes(BitPackerMemberAttribute attribute, Endianness defaultEndianness)
        {
            this.attribute = attribute;
            this.defaultEndianness = defaultEndianness;
        }
    }

    internal class PropertyDetails : PropertyAttributes
    {
        private readonly PropertyInfo propertyInfo;
        private readonly ObjectDetails objectDetails;

        public PropertyInfo PropertyInfo
        {
            get { return this.propertyInfo; }
        }

        public Type Type
        {
            get { return this.propertyInfo.PropertyType; }
        }

        public ObjectDetails ObjectDetails
        {
            get { return this.objectDetails; }
        }

        public PropertyDetails(PropertyInfo propertyInfo, BitPackerMemberAttribute attribute, Endianness defaultEndianness)
            : base(attribute, defaultEndianness)
        {
            this.propertyInfo = propertyInfo;

            this.objectDetails = new ObjectDetails(this.propertyInfo.PropertyType, attribute, this.Endianness);
        }

        public void Discover()
        {
            this.objectDetails.Discover();
        }
    }
}
