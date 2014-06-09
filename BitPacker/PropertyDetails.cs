﻿using System;
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
        protected readonly PropertyInfo lengthProperty;

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

        public PropertyInfo LengthProperty
        {
            get { return this.lengthProperty; }
        }

        public PropertyDetails(Type parentType, PropertyInfo propertyInfo, BitPackerMemberAttribute attribute, Endianness defaultEndianness)
            : base(attribute, defaultEndianness)
        {
            this.propertyInfo = propertyInfo;

            if (this.attribute.LengthField != null)
            {
                this.lengthProperty = parentType.GetProperty(this.attribute.LengthField, BindingFlags.Public | BindingFlags.Instance);
                if (this.lengthProperty == null)
                    throw new Exception(String.Format("Could not find length field {0}", this.attribute.LengthField));
                if (this.lengthProperty.PropertyType != typeof(int))
                    throw new Exception(String.Format("Length field {0} must have type int", this.attribute.LengthField));
            }
        }
    }
}
