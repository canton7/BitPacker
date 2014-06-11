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

        public int EnumerableLength
        {
            get { return this.attribute.Length; }
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
        private readonly ObjectDetails elementObjectDetails;

        public PropertyInfo PropertyInfo
        {
            get { return this.propertyInfo; }
        }

        public Type Type
        {
            get { return this.propertyInfo.PropertyType; }
        }

        public bool IsEnumable
        {
            get { return this.Type.IsArray || typeof(IEnumerable<>).IsAssignableFrom(this.Type); }
        }

        public Type ElementType
        {
            get
            {
                if (!this.IsEnumable)
                    throw new InvalidOperationException("Not Enumerable");
                return this.Type.IsArray ? this.Type.GetElementType() : this.Type.GetGenericArguments()[0];
            }
        }

        public ObjectDetails ObjectDetails
        {
            get { return this.objectDetails; }
        }

        public ObjectDetails ElementObjectDetails
        {
            get { return this.elementObjectDetails; }
        }

        public PropertyDetails(PropertyInfo propertyInfo, BitPackerMemberAttribute attribute, Endianness defaultEndianness)
            : base(attribute, defaultEndianness)
        {
            this.propertyInfo = propertyInfo;

            this.objectDetails = new ObjectDetails(this.propertyInfo.PropertyType, this.Endianness);
            if (this.IsEnumable)
                this.elementObjectDetails = new ObjectDetails(this.ElementType, this.Endianness);
        }

        public void Discover()
        {
            this.objectDetails.Discover();
            if (this.elementObjectDetails != null)
                this.elementObjectDetails.Discover();
        }
    }
}
